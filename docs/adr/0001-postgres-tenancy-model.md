# ADR-001: Tenancy model on PostgreSQL

- **Status:** Accepted
- **Date:** 2026-07-21
- **Deciders:** Serhat Alaftekin
- **Phase:** 0 (implemented in Phase 2)

## Context and Problem Statement

TerminWise is a multi-tenant SaaS: many small businesses (tenants) share one platform.
Their data must be **strongly isolated** — a bug, a forgotten `WHERE`, or a raw SQL query
must never leak one tenant's appointments to another. We run a **modular monolith on a
single PostgreSQL instance** (EF Core + Npgsql), evolving toward service extraction later.

How should tenant data be partitioned in PostgreSQL, and how is isolation enforced so that
it holds even when application code is wrong?

## Decision Drivers

- **Isolation strength** — ideally enforced by the database, not only by app code (defense in depth).
- **Operational simplicity** — this is a reference architecture run on modest infra; N databases/schemas per tenant is real ops cost (migrations, connections, backups).
- **EF Core support** — first-class, documented support beats fighting the ORM.
- **Cost & density** — many small tenants; per-tenant infrastructure scales poorly and expensively.
- **Demonstrability** — the repo must *show* the isolation guarantee with an integration test (Phase 2 acceptance: "cross-tenant access provably blocked").
- **Migration/onboarding cost** — adding a tenant should be a row, not a provisioning job.

## Considered Options

1. **Database per tenant** — one PostgreSQL database per tenant.
2. **Schema per tenant** — one schema per tenant inside a shared database.
3. **Shared schema + tenant discriminator** — one `tenant_id` column, app-level filtering (EF Core global query filter).
4. **Shared schema + discriminator + PostgreSQL Row Level Security (RLS)** — option 3, backstopped by database-enforced RLS policies. **← chosen**

## Decision Outcome

**Chosen option: "4 — Shared schema + tenant discriminator, backstopped by PostgreSQL Row Level Security."**

Every tenant-owned table carries a `tenant_id` column. Two layers enforce isolation:

- **Application layer (ergonomics + fail-safe):** an EF Core **global query filter** (`tenant_id == currentTenant`) is applied to every tenant entity, so developers get correct filtering by default without writing it per query. On EF Core 10 this can be a **named** filter, composed with soft-delete independently.
- **Database layer (the real boundary):** **RLS policies** on every tenant table, keyed off a session setting (`current_setting('app.current_tenant')`). The application connects as a **non-owner role without `BYPASSRLS`** (and tables use `FORCE ROW LEVEL SECURITY`), so even raw SQL or a forgotten filter cannot cross tenants — the database refuses.

The current tenant is injected per request (tenant resolution middleware, Phase 2) and pushed to the DB session with `set_config('app.current_tenant', <id>, true)` — i.e. `SET LOCAL`, **transaction-scoped** — at the start of each unit of work.

This is chosen because it delivers the **strongest, DB-enforced isolation at the lowest operational cost**, keeps the highest tenant density, matches EF Core's first-class support, and makes the Phase 2 "cross-tenant access provably blocked" test trivial to write against a real guarantee rather than a hopeful one. It also directly demonstrates a senior-level pattern (belt-and-suspenders isolation) — which is the point of the repo.

### Consequences

- **Good — isolation survives buggy code.** RLS is enforced by PostgreSQL, so a missing filter, an `IgnoreQueryFilters()`, or hand-written SQL still cannot leak across tenants.
- **Good — cheapest ops & highest density.** One database, one schema, one migration path; onboarding a tenant is an `INSERT`, not provisioning.
- **Good — clean test story.** A single integration test (Testcontainers PostgreSQL) sets tenant A, queries, switches to tenant B, and asserts zero cross-visibility — proving the DB boundary, not just app logic.
- **Good — table classification is enforced, not conventional.** A Phase 2 test inspects the schema and **fails if any table is neither RLS-enabled (with `FORCE`) nor on an explicit non-tenant allowlist** — tenant catalog/registry, Quartz.NET job-store tables, EF Core migrations-history table (extended as needed). Every new table must be consciously classified as tenant-owned or shared; nothing is isolated (or exempted) by accident.
- **Trade-off — connection-pool discipline is mandatory.** The tenant must be set with `set_config(..., true)`/`SET LOCAL` inside the transaction so it is transaction-scoped and auto-discarded; Npgsql's default `DISCARD ALL` reset on pool return must stay on, and **`No Reset On Close` must never be used** with a session-level tenant variable, or a pooled connection could carry one tenant's context to the next. (Npgsql pooling reset; PostgreSQL `set_config`.)
- **Trade-off — the app DB role must not own the tables and must not have `BYPASSRLS`;** migrations run as a separate privileged owner/migration role. This role separation has to be set up deliberately. Because that migration role owns the tables and **bypasses RLS by design**, migrations must never contain data-touching statements that assume a tenant context (no tenant-scoped `INSERT`/`UPDATE`/`DELETE` in migrations — RLS will not scope them, so they would run unfiltered across all tenants).
- **Trade-off — noisy-neighbor and "blast radius" are shared.** One tenant's heavy load or a table-wide corruption affects all; per-tenant physical isolation is intentionally traded away (revisit for enterprise/regulated tenants — a future ADR could allow a hybrid where a premium tenant gets its own database via the same `ITenantStore` abstraction).
- **Follow-up (Phase 2):** implement tenant resolution middleware, tenant-scoped `DbContext` (scoped factory to play well with pooling), the app/migration role split, and the RLS policy migrations. A central tenant catalog ("AdminDb") maps tenant → id.

## Pros and Cons of the Options

### 1. Database per tenant

- 👍 Strongest physical isolation; trivial per-tenant backup/restore, per-tenant scaling, and "delete a tenant" = drop a database.
- 👍 No risk of cross-tenant query leakage by construction.
- 👎 Operationally heavy at scale: N migrations, N connection pools, provisioning on onboarding — disproportionate for many *small* tenants.
- 👎 Cross-tenant analytics/admin queries become hard; connection management gets complex.
- 👎 Overkill for a modular-monolith reference app; obscures rather than demonstrates the interesting isolation techniques.

### 2. Schema per tenant

- 👍 Logical isolation within one database; per-tenant schema is a middle ground.
- 👎 **Not directly supported by EF Core and explicitly not recommended** (EF Core multi-tenancy docs) — you fight the ORM (dynamic schema switching, migrations per schema).
- 👎 Migration fan-out: every schema change applies to every schema; hundreds of schemas is an operational smell.
- 👎 Still needs care to prevent `search_path`/schema-qualification mistakes leaking data.

### 3. Shared schema + tenant discriminator (app-level only)

- 👍 Simplest; one schema, one migration; EF Core global query filter is first-class and ergonomic.
- 👍 Highest density, lowest cost.
- 👎 **Isolation is only as good as the application code.** A forgotten filter, `IgnoreQueryFilters()`, raw SQL, or a reporting query can leak across tenants — the boundary is a convention, not a guarantee.
- 👎 Weak security story for a portfolio that's meant to demonstrate rigor; hard to claim "provably isolated."

### 4. Shared schema + discriminator + RLS (chosen)

- 👍 Keeps option 3's simplicity, density, and EF ergonomics, **and** adds a real, DB-enforced boundary.
- 👍 "Provably blocked" is literally provable: the database, not the app, rejects cross-tenant access.
- 👍 Excellent teaching/interview artifact — shows RLS, role separation, and pooling-safe session state.
- 👎 Requires connection-pool discipline (`SET LOCAL`) and DB role separation (non-owner, no `BYPASSRLS`, `FORCE RLS`).
- 👎 Shared blast radius / noisy-neighbor remain (accepted; revisit for premium tenants).

## More Information

Sources consulted (verified 2026-07-21):

- EF Core — [Multi-tenancy](https://learn.microsoft.com/ef/core/miscellaneous/multitenancy): support matrix (discriminator ✅ via global query filter, database-per-tenant ✅ via config, schema-per-tenant ⚠️ not supported / not recommended); scoped `DbContextFactory` for per-request tenant.
- EF Core — [Global Query Filters](https://learn.microsoft.com/ef/core/querying/filters) and [What's new in EF Core 10 — named query filters](https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew#named-query-filters).
- EF Core — [DbContext pooling with state](https://learn.microsoft.com/ef/core/performance/advanced-performance-topics#dbcontext-pooling): tenant injected via scoped factory (OnConfiguring runs once).
- PostgreSQL — [Row Security Policies](https://www.postgresql.org/docs/current/ddl-rowsecurity.html): `ENABLE`/`FORCE ROW LEVEL SECURITY`, `BYPASSRLS`, owner/superuser bypass, `USING`/`WITH CHECK`.
- PostgreSQL — [set_config / current_setting](https://www.postgresql.org/docs/current/functions-admin.html): `set_config(name, value, is_local=true)` == `SET LOCAL` (transaction-scoped, auto-reset at COMMIT/ROLLBACK).
- Npgsql — [Performance / pooled connection reset](https://www.npgsql.org/doc/performance.html) and [connection string parameters](https://www.npgsql.org/doc/connection-string-parameters.html): default `DISCARD ALL` on pool return; `No Reset On Close` disables it (do not use with session-level tenant state).

Related: ADR-002 (Keycloak tenancy — the tenant claim that feeds `app.current_tenant`), and a future ADR could introduce a hybrid "premium tenant gets its own database" behind the same tenant-store abstraction.
