# ADR-002: Keycloak tenancy model

- **Status:** Proposed
- **Date:** 2026-07-21
- **Deciders:** Serhat Alaftekin
- **Phase:** 0 (implemented in Phase 2)

## Context and Problem Statement

TerminWise uses Keycloak for identity. Each tenant (a small business) has its own users
— owners, staff — who must authenticate and receive a token that tells the platform
**which tenant they belong to**. That tenant identity then drives everything downstream:
authorization, and the database session variable `app.current_tenant` that RLS keys off
(see [ADR-001](./0001-postgres-tenancy-model.md)).

How should tenants be modeled in Keycloak so that a login yields a trustworthy tenant
identity in the token, without turning identity into an operational burden as tenants grow?

## Decision Drivers

- **Tenant identity in the token** — the token must carry a reliable tenant id the API can trust and map to `app.current_tenant`.
- **Operational simplicity at scale** — many small tenants; per-tenant identity infrastructure (keys, config, admin) is costly and Keycloak realms are not designed to exist in large numbers.
- **Enterprise-readiness** — a tenant should be able to bring its own SSO (federate via their IdP) without reconfiguring the whole platform.
- **Coherence with ADR-001** — the identity model should feed the data-tenancy model cleanly (org/tenant claim → `app.current_tenant`).
- **Boring, well-supported building blocks** — prefer a first-class, GA Keycloak feature over a hand-rolled convention.
- **Demonstrability** — the login → tenant-scoped token → RLS-enforced query flow must be showable end to end.

## Considered Options

1. **Realm per tenant** — each tenant gets its own Keycloak realm.
2. **Single realm + groups/attributes** — one realm; model tenancy manually with a group or user attribute carrying the tenant id (the pre-Organizations pattern).
3. **Single realm + Keycloak Organizations** — one realm; each tenant is an *Organization*; membership and the `organization` token claim provide tenant identity. **← chosen**

## Decision Outcome

**Chosen option: "3 — Single realm + Keycloak Organizations."**

One realm holds the whole platform. Each tenant is a Keycloak **Organization**; users are
**members** of their organization. Clients request the **`organization` scope**, so tokens
carry an `organization` claim containing the tenant's organization id and attributes. The
API extracts that id as the tenant identity and pushes it into the DB session as
`app.current_tenant` — closing the loop with ADR-001's RLS. Per-organization **identity
brokering** lets a tenant federate its own IdP (enterprise SSO) with automatic redirection
by email domain, and **identity-first login** routes users into the right organization
context.

This is chosen because it puts a **trustworthy tenant id in the token via a purpose-built,
GA feature**, keeps identity operations flat (one realm to run, patch, and reason about) at
high tenant density, and gives an enterprise-SSO story for free — all while composing
cleanly with the data-tenancy decision. It is the identity-layer analogue of ADR-001:
shared infrastructure, strong logical isolation, purpose-built features over per-tenant
physical separation.

### Consequences

- **Good — one coherent tenant-context pipeline.** `login → organization claim → API → app.current_tenant → RLS`. The Phase 2 acceptance ("login via Keycloak; role-restricted endpoints enforced"; "cross-tenant access provably blocked") is demonstrated across identity *and* data with one flow.
- **Good — flat identity ops.** A new tenant is a new Organization (an API call), not a new realm to provision, key-manage, and migrate. This matches the "many small tenants" reality and avoids the realm-count anti-pattern.
- **Good — enterprise SSO per tenant.** Each organization can link its own IdP (an IdP binds to exactly one organization) with domain-based auto-redirect — a tenant can bring Entra ID / Google Workspace without touching other tenants.
- **Trade-off — shared realm config = shared blast radius.** Login themes, password policies, token lifetimes, and realm keys are shared; a realm-level misconfiguration or compromise affects all tenants. Per-organization customization exists (branding, auth steps, IdP) but is narrower than full realm isolation. (Consistent with the isolation trade-off already accepted in ADR-001.)
- **Trade-off — token-claim trust is now security-critical.** The tenant id arrives from the token, so the API must validate it (signature, `organization` scope present, membership) before trusting it for `app.current_tenant`; a spoofed or missing claim must fail closed. RLS is the backstop if this is ever wrong.
- **Trade-off — version floor and relative newness.** Requires **Keycloak 26.x** (Organizations is GA since 26.0, actively enhanced through 26.7); the feature must be **enabled** (server feature + per-realm Organizations setting). It is younger than realm-based tenancy, so operational patterns and tooling are still maturing — pin the version and track release notes.
- **Follow-up (Phase 2):** enable Organizations; model tenant = organization; add the `organization` scope to the API client; map the org id to the tenant-id claim the middleware reads; wire policy-based authorization (roles/permissions). The AI service's least-privilege service account (Phase 4.5, ADR-008) also lives in this realm.

## Pros and Cons of the Options

### 1. Realm per tenant

- 👍 Strongest isolation: separate realm keys, config, themes, and admin delegation per tenant; a tenant's identity data is fully partitioned.
- 👍 Full per-tenant customization of every realm setting.
- 👎 Keycloak realms are **not designed to exist in large numbers**; hundreds/thousands of realms is an operational and performance anti-pattern (per-realm keys, caches, admin, migrations).
- 👎 Onboarding a tenant becomes provisioning a realm; cross-tenant platform administration and shared clients get complex.
- 👎 Disproportionate for many *small* tenants — the identity-layer twin of the "database per tenant" cost rejected in ADR-001.

### 2. Single realm + groups/attributes

- 👍 One realm (flat ops); simple; works on any Keycloak version; tenant id via a group path or user attribute mapped into the token.
- 👍 Full control over the tenant model.
- 👎 **Roll-your-own multi-tenancy:** membership, invitations, per-tenant IdP, and identity-first login must be built and maintained by hand — reinventing what Organizations now provides out of the box.
- 👎 No first-class per-tenant IdP brokering; weaker enterprise-SSO story.
- 👎 More custom code = more to get subtly wrong on a security-critical path.

### 3. Single realm + Keycloak Organizations (chosen)

- 👍 Purpose-built, **GA (fully supported since Keycloak 26)** B2B multi-tenancy: members, invitations, per-org IdP brokering, identity-first login, and an `organization` token claim.
- 👍 Flat ops + high tenant density; onboarding is an API call.
- 👍 Clean composition with ADR-001 (org claim → `app.current_tenant`); strong end-to-end demo.
- 👎 Requires Keycloak 26.x and explicit enablement; relatively new, tooling/patterns still maturing.
- 👎 Shared realm config / blast radius (accepted, per ADR-001).

## More Information

Sources consulted (verified 2026-07-21, Keycloak 26.x; latest 26.7.0):

- Keycloak Server Admin — [Managing Organizations (intro)](https://www.keycloak.org/docs/latest/server_admin/index.html): "provides some of the core capabilities needed to manage organizations… manage members, onboard via invitation links, federate identities through identity brokering, identity-first login and organization-specific auth steps, propagate organization-specific claims to applications through tokens."
- Keycloak — Organization token claim: the `organization` claim (org id + attributes) is added to tokens when the **`organization` scope** is requested — e.g. `{"organization": {"testcorp": {"id": "…"}}}`.
- Keycloak — Managing identity providers for organizations: **"An identity provider can only be linked to a single organization,"** with domain-based auto-redirect (`Redirect when email domain matches`, `Any`).
- Keycloak — Authenticating members: `Organization Identity-First Login` execution with the `Requires user membership` setting (fail if the user is not a member of any organization).
- Keycloak Release Notes — [26.0.0](https://www.keycloak.org/docs/latest/release_notes/index.html): **"Starting with Keycloak 26, the Organizations feature is fully supported."** Enhancements since: organization groups (26.6), fine-grained org-admin delegation (26.7).

Related: [ADR-001](./0001-postgres-tenancy-model.md) (the `organization` claim feeds `app.current_tenant`); ADR-008 (Phase 4.5 — least-privilege service account for the AI agent lives in this realm).
