# Mini SaaS Reference Architecture — Project Roadmap

> Open-source portfolio project: a **multi-tenant, event-driven SaaS reference architecture** built with .NET, Angular, and Azure. The domain (appointment/reservation management for small businesses — e.g. barbershops, physiotherapists, studios) is intentionally simple; **the architecture is the star**.

## Goals

- Demonstrate senior/architect-level decision making: every significant choice is documented as an ADR.
- Each phase ends in a **demonstrable, working state** — `main` is always green, README always current.
- Produce reusable content: code examples feed planned Medium articles (Keycloak, SSE vs. WebSocket, OpenTelemetry).

## Tech Stack

| Area | Choice |
|---|---|
| Backend | .NET (latest LTS), Clean Architecture, custom CQRS (no MediatR — deliberate, see ADR-004), DDD |
| Validation | **FluentValidation** (free/OSS), wired as a validation pipeline behavior in the custom CQRS dispatcher |
| Frontend | Angular 22 (zoneless, OnPush default), standalone components, **Signal Forms** (stable since v22) |
| Frontend state | **NgRx SignalStore** as the default; one module deliberately in classic NgRx (Store/Effects) as a documented comparison (ADR-009) |
| UI / CSS | **Tailwind CSS v4 + Angular Material** (Material for calendar/datepicker-heavy widgets, Tailwind for layout/custom styling); PrimeNG considered in ADR-010 |
| Identity | Keycloak (tenancy approach decided in ADR-002) |
| Data | **PostgreSQL** (EF Core + Npgsql) — tenancy model decided in ADR-001 (incl. Postgres Row Level Security option) |
| Cache | Redis (StackExchange.Redis), cache-aside, tenant-prefixed keys |
| Messaging | RabbitMQ; client library decided in ADR-003 (thin custom abstraction over `RabbitMQ.Client` preferred; MassTransit v9+ commercial, Rebus as OSS alternative; Kafka comparison included) |
| Search | Elasticsearch (tenant isolation strategy in ADR) |
| AI | **Standalone AI service from day one** — **Microsoft Agent Framework** (1.0 GA, .NET) + **Azure AI Foundry** (model via Foundry catalog — Claude/GPT selectable, code stays model-agnostic); tool calling against the platform's public API, SSE streaming |
| Background jobs | Custom `BackgroundService` for the outbox dispatcher; **Quartz.NET** (Apache 2.0) with PostgreSQL persistent job store + clustering for scheduled jobs (appointment reminders); graceful shutdown via cancellation tokens (ADR-014) |
| Error handling | Custom **Result pattern** (`Result<T>`): handlers return Results, no exceptions for control flow; mapped to RFC 7807 problem details (ADR-011) |
| Idempotency | Messaging: Inbox pattern (Phase 3). HTTP: `Idempotency-Key` header middleware for POST operations, Redis-backed (Phase 2) |
| Observability | **OpenTelemetry for all three signals** — logs via native `ILogger` + OTel Logs (no separate logging framework), metrics, traces — exported over OTLP. Local/default backend: **Grafana stack** (Prometheus, Loki, Tempo, Grafana, docker-compose); optional Azure export target: Application Insights (ADR-012) |
| Testing (backend) | xUnit (unit), **ArchUnitNET** (architecture rules), Testcontainers (integration: DB, RabbitMQ, Redis, Elasticsearch, Keycloak) |
| Testing (frontend) | **Vitest** (unit — Angular's default runner since v21), **Playwright** (e2e) |
| IaC / Deploy | Terraform → Azure, CI/CD via GitHub Actions |

## Working Rules (for Claude Code)

1. **Never start a phase before the previous one is complete.** A phase is complete when its acceptance criteria pass and the README section for it is written.
2. **ADR-first:** any decision listed for a phase gets an ADR in `docs/adr/` *before* the implementing code is merged. Format: context → options considered → decision → consequences.
3. Keep `main` deployable at all times. Feature work happens on branches.
4. Prefer boring, well-documented solutions over clever ones — this repo is meant to be read.
5. Every module ships with tests per the Testing Strategy below.

## Testing Strategy

- **Architecture tests (ArchUnitNET)** run in CI from Phase 1 onward and enforce the Clean Architecture rules as executable law: Domain references nothing; Application references only Domain; Infrastructure is unreferenced by Domain/Application; modules only talk to each other via public contracts/events (no cross-module internal references); every command has exactly one handler; naming conventions for handlers/validators.
- **Backend unit tests (xUnit):** domain logic (availability rules, booking invariants, value objects) tested without infrastructure. Aim for meaningful coverage of the Domain layer, not a vanity percentage.
- **Backend integration tests (Testcontainers):** real PostgreSQL, RabbitMQ, Redis, Elasticsearch, and Keycloak spun up in containers. This is where tenant isolation, outbox/inbox reliability, idempotency, and cache invalidation are proven — the phase acceptance criteria referencing "integration test" run here.
- **Frontend unit tests (Vitest):** Angular's default runner since v21 — component logic, SignalStore stores (methods, computed state), Signal Forms validation schemas.
- **Frontend e2e tests (Playwright):** one smoke suite from Phase 1 (login → create service → book appointment), extended per phase; Phase 4.5 adds the chat-books-an-appointment flow as an e2e test. Runs against a docker-compose environment in CI.
- **Test pyramid discipline:** e2e stays thin (happy paths + critical flows); breadth lives in unit and architecture tests; reliability guarantees live in integration tests.

---

## Phase 0 — Foundation & ADRs (~1 week)

**Scope**
- **Repo bootstrap:** create the GitHub repo (public); `.gitignore` (Visual Studio/.NET + Node/Angular combined); `.editorconfig` (C# + TS conventions); **LICENSE — MIT** (right choice for a portfolio reference repo: maximally permissive, no adoption friction); branch protection on `main` (PRs + green CI required); solution scaffold (`src/`, `tests/`, `frontend/`, `docs/adr/`, `docs/diagrams/`, `docker-compose.yml` placeholder).
- Repository structure (solution layout, `docs/adr/`, `docs/diagrams/`), README with project vision.
- CI pipeline skeleton (build + test on PR).
- Initial ADRs:
  - **ADR-001:** Tenancy model on PostgreSQL — database-per-tenant vs. schema-per-tenant vs. shared schema with tenant discriminator vs. shared schema + **Row Level Security** (comparative, with trade-offs).
  - **ADR-002:** Keycloak tenancy — realm-per-tenant vs. single realm + organizations/groups.
  - **ADR-003:** Message broker & client library — RabbitMQ vs. Kafka for this workload (classic messaging vs. event streaming). Library dimension incl. licensing: MassTransit v9+ is commercial (v8 stays OSS, security patches through 2026); alternatives: thin custom abstraction over `RabbitMQ.Client`, or Rebus (free/OSS). Preference: own thin abstraction, consistent with the custom-CQRS ethos.
  - **ADR-004:** Custom CQRS + **modular monolith first, microservices by extraction** — why no MediatR (v13+ is dual-licensed RPL-1.5/commercial with a free Community tier; not used here both to keep the reference repo dependency-light for corporate adopters and to demonstrate the mediator/pipeline pattern from first principles), and why we start as a modular monolith with module boundaries designed as future service boundaries, then extract services in Phase 6 rather than starting distributed.

**Acceptance criteria**
- Repo builds in CI; four ADRs merged; README explains vision and architecture at a glance.

## Phase 1 — Modular Monolith MVP (~2–3 weeks)

**Scope**
- Clean Architecture skeleton with custom CQRS (command/query dispatchers, pipeline behaviors for validation/logging).
- **FluentValidation** integrated as a validation pipeline behavior: every command passes through registered validators before reaching its handler; validation failures map to consistent API error responses (RFC 7807 problem details).
- **Result pattern:** lightweight custom `Result<T>`/`Error` types; command/query handlers return Results (no exceptions for expected failures); a single mapping layer converts Results — including FluentValidation failures — to RFC 7807 responses. **New ADR-011:** error handling strategy (Result pattern vs. exceptions; custom type vs. ErrorOr/FluentResults).
- Single module: **Booking** — business profile, services, staff, availability rules, appointment creation/cancellation. Module boundaries are designed as **future service boundaries** from day one: modules expose only public contracts and events, own their DB schema exclusively, and never reference another module's internals (enforced by architecture tests) — this is what makes Phase 6 extraction cheap and real.
- EF Core persistence; domain model with DDD tactical patterns (aggregates, value objects, domain events raised in-process for now).
- Angular 22 scaffold: zoneless, OnPush, standalone components, Tailwind CSS v4 + Angular Material theme.
- Admin panel: manage services/staff/availability, view appointments.
  - All forms built with **Signal Forms** (typed schema, validation, Submission API) — no Reactive Forms except where a third-party CVA requires interop.
  - Feature state via **NgRx SignalStore** (one store per feature; `withEntities`, `withMethods`, resource-based data loading via `httpResource`).
- **New ADR-009:** Frontend state management — NgRx SignalStore vs. classic NgRx Store/Effects vs. plain signals; decision: SignalStore as default, with one module (e.g. Appointments list) intentionally implemented in classic NgRx as a side-by-side reference.
- **New ADR-010:** UI foundation — Tailwind v4 + Angular Material vs. PrimeNG vs. headless (spartan/ui); trade-offs around calendar/scheduler widgets.

**Acceptance criteria**
- End-to-end demo: create a business, define availability, book an appointment via the UI.
- Domain unit tests for availability/booking rules.
- Architecture test suite (ArchUnitNET) green in CI and failing demonstrably when a layer rule is violated.
- Vitest unit tests for SignalStore stores and Signal Forms schemas; Playwright smoke suite (login → create service → book appointment) green in CI.
- All forms are Signal Forms; feature state lives in SignalStore (verified in code review — no ad-hoc component state for shared data).
- ADR-009 and ADR-010 merged before the corresponding frontend code.

## Phase 2 — Multi-Tenancy, Keycloak & Redis (~2–3 weeks)

**Scope**
- Tenant resolution middleware (subdomain or header based), tenant-scoped DbContext, central registry ("AdminDb"-style tenant catalog).
- Keycloak integration per ADR-002: roles/permissions model, tenant claim in tokens, policy-based authorization in the API.
- Redis cache-aside: tenant configuration (hot path in tenant resolution) and availability queries. Tenant-prefixed keys (`tenant:{id}:*`), explicit invalidation strategy (event-driven invalidation wired fully in Phase 3).
- **HTTP idempotency middleware:** POST operations (esp. appointment creation) accept an `Idempotency-Key` header; key + response cached in Redis with TTL; replayed requests return the original response instead of re-executing. Tenant-scoped keys.
- **New ADR-005:** Cache strategy — what is cached, TTLs, invalidation triggers, stampede protection.

**Acceptance criteria**
- Two tenants running side by side with full data isolation; cross-tenant access provably blocked (integration test).
- Login via Keycloak; role-restricted endpoints enforced.
- Cache hit/miss visible in logs/metrics; invalidation works on config change.
- Replaying a POST with the same `Idempotency-Key` does not create a duplicate appointment (integration test).

## Phase 3 — Event-Driven Expansion: RabbitMQ + Outbox (~2–3 weeks)

**Scope**
- Second module: **Notifications** (email/log-based confirmations and reminders).
- Domain events → **Outbox pattern** → RabbitMQ (client library per ADR-003 — thin custom abstraction over `RabbitMQ.Client`, or Rebus) → consumers with **Inbox/idempotency**, retry policy, dead-letter handling.
- Event-driven cache invalidation: `AppointmentCreated` → drop availability cache.
- Competing consumers scenario documented and tested.
- **Background jobs, made explicit:**
  - **Outbox dispatcher** as a custom `BackgroundService`: polls unprocessed outbox rows, publishes, marks processed only after successful publish. Crash-safe by design: restart resumes from unprocessed rows; at-least-once delivery accepted, deduplicated by consumer Inbox.
  - **Scheduled jobs via Quartz.NET** with PostgreSQL persistent job store: **appointment reminders** ("your appointment is tomorrow at 14:00") as the domain use case. Schedules survive restarts; misfire policy defined; clustering enabled so a job never runs concurrently on two instances (matters from Phase 6 onward). Reminder job is idempotent (`ReminderSentAt` marker — a crashed-and-rerun job never double-notifies).
  - **Graceful shutdown** everywhere: hosted services honor cancellation tokens — finish in-flight work, take no new work, drain within the shutdown timeout.
  - **New ADR-014:** background job strategy — custom BackgroundService (outbox) + Quartz.NET (scheduling) vs. Hangfire (free LGPL core, paid Pro/dashboard) vs. all-custom; failure semantics per job type (at-least-once + idempotency as the universal answer).
- **New ADR-006:** Reliable messaging — outbox/inbox, idempotency keys, poison message handling.

**Acceptance criteria**
- Killing the consumer mid-processing loses no messages; duplicates are not double-processed (integration tests).
- Booking an appointment triggers a notification through the full outbox → broker → consumer path.
- Killing the app mid-outbox-dispatch loses no events: unprocessed rows are picked up on restart (integration test).
- Reminder scheduling survives an application restart (Quartz persistent store); re-running a reminder job does not double-notify (idempotency test).

## Phase 4 — Elasticsearch + Observability (~2 weeks)

**Scope**
- Search for businesses/services: index design, analyzers, tenant isolation (index-per-tenant vs. filtered alias — **ADR-007**).
- Sync strategy: index updated via the existing event pipeline (no dual-write).
- Observability via **OpenTelemetry** end-to-end: tracing across API → broker → consumer → Elasticsearch.
  - Local/default backend: **Grafana stack in docker-compose** — logs via native `ILogger` + **OTel Logs** → OTLP → **Loki** (automatic trace/span correlation on every log line), metrics → **Prometheus** (request rates, latency histograms, queue depth, cache hit ratio, outbox lag), traces → **Tempo**, dashboards in **Grafana** (committed to the repo as JSON).
  - **New ADR-012:** observability stack — all three signals through OpenTelemetry/OTLP (no separate logging framework; Serilog considered and rejected for pipeline unity); Grafana stack locally vs. Application Insights in Azure; note that Prometheus handles metrics while Loki handles logs (division of responsibilities).

**Acceptance criteria**
- Search returns tenant-scoped results only; relevance sanity-checked.
- A single trace visibly spans HTTP request → published event → consumer (viewable in Grafana/Tempo).
- Grafana dashboards show live metrics (Prometheus) and correlated logs (Loki) for a booking flow.

## Phase 4.5 — AI Booking Assistant (~2–3 weeks)

**Scope**
- **Built as a standalone service from day one** (own project, own deployable, stateless, independently scalable): the AI assistant never lives inside the monolith. It talks to the platform exclusively through the public API — same contract any third-party client would use.
- Customer-facing chat: natural language → **Microsoft Agent Framework** (1.0 GA, .NET) agent with **tool calling** against the platform API ("Is there a haircut slot tomorrow afternoon?" → query availability → propose slots → confirm → create booking). Model served via **Azure AI Foundry** model catalog (Claude/GPT selectable — agent code stays model-agnostic).
- Streaming responses via **SSE**.
- Security architecture: the AI service calls the platform API with a **tenant-scoped, least-privilege token** (Keycloak service account / token exchange) — **ADR-008: authorization model for AI agents**.
- **New ADR-008b:** AI stack — Microsoft Agent Framework + Azure AI Foundry vs. direct model API; trade-offs (Azure dependency for local dev and token costs vs. model-agnostic code, enterprise alignment, built-in observability/eval tooling).
- Guardrails: tool-level validation, no free-form data access, prompt-injection considerations documented.
- Optional stretch: owner-facing analytics Q&A ("which service had the most cancellations last month?") via structured tool calls.
- Optional stretch (frontend): Angular 22's experimental **WebMCP** — expose the booking form as a typed tool callable by in-browser agents; pairs naturally with the Signal Forms schema already defined in Phase 1.

**Acceptance criteria**
- Full demo: one chat message books a real appointment end-to-end (this becomes the README hero GIF).
- Assistant provably cannot read or mutate another tenant's data (test).
- The AI service runs and scales independently: `docker-compose up` starts it as its own container; killing it leaves the platform fully functional (chat unavailable, everything else unaffected).

## Phase 5 — Terraform, Azure Deploy & Content Harvest (~2 weeks)

**Scope**
- Terraform: App Service/Container Apps, **Azure Database for PostgreSQL**, Redis, Service Bus-or-RabbitMQ hosting decision for cloud, Elasticsearch hosting, **Azure AI Foundry resources** for the AI service, Key Vault, Application Insights (optional OTel export target per ADR-012 — swapping observability backends is configuration, not code).
- CI/CD: build → test → deploy pipeline; environment configuration strategy.
- Architecture diagram(s) in README; polish all ADRs; contribution guide.
- Content harvest: extract code examples for the planned Medium articles (Keycloak guide, SSE vs. WebSocket, gRPC vs. REST if applicable, OpenTelemetry follow-up).

**Acceptance criteria**
- `terraform apply` from a clean state produces a working public deployment.
- README top section: hero GIF (AI assistant booking), architecture diagram, phase-by-phase changelog.

---

## Phase 6 — Microservice Extraction + API Gateway (~2–3 weeks)

**Scope**
- Extract **Notifications** into a standalone deployable service: own database, own CI job, communicates with the monolith exclusively via RabbitMQ events (which it already does — the extraction should require near-zero changes to the Booking module, proving the boundary was real).
- The **AI assistant** is already standalone since Phase 4.5 — Phase 6 brings it behind the gateway alongside the rest. The ADR contrasts the two paths: AI was *designed* separate (known scaling profile), Notifications was *extracted* (boundary matured inside the monolith).
- **YARP API Gateway** in front: routing to monolith, notifications, and AI services; tenant-aware request forwarding; token validation at the edge.
- Service-to-service auth: Keycloak client credentials flow, least-privilege scopes per service.
- Contract tests between monolith and extracted services (event schemas as the contract).
- **New ADR-013:** service extraction — what was extracted and why, what deliberately stays in the monolith (Booking), extraction criteria (independent scaling, independent deployment cadence, team boundaries), and the cost accepted (operational complexity, distributed debugging).

**Acceptance criteria**
- `docker-compose up` runs the full distributed system: gateway + monolith + notifications service + AI service + infrastructure.
- Booking module code diff for the extraction is minimal (documented in the ADR as evidence of boundary quality).
- A single OTel trace spans gateway → monolith → RabbitMQ → notifications service.
- Killing the notifications service does not break booking (graceful degradation; messages queue up and drain on restart).

---

## Timeline Summary

| Phase | Focus | Est. |
|---|---|---|
| 0 | Foundation + ADRs | 1 wk |
| 1 | Modular monolith MVP (Booking) | 2–3 wks |
| 2 | Multi-tenancy + Keycloak + Redis | 2–3 wks |
| 3 | RabbitMQ + Outbox/Inbox | 2–3 wks |
| 4 | Elasticsearch + OpenTelemetry | 2 wks |
| 4.5 | AI booking assistant | 2–3 wks |
| 5 | Terraform + Azure + content | 2 wks |
| 6 | Microservice extraction + YARP gateway | 2–3 wks |

**Total: ~4 months** at evening/weekend pace.
