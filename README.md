# TerminWise

> **Multi-tenant, event-driven SaaS reference architecture** — a .NET modular monolith that
> evolves into microservices by extraction. Clean Architecture, custom CQRS, Keycloak,
> RabbitMQ + Outbox, Angular 22 Signal Forms, and an AI booking assistant.
>
> The domain (appointment/reservation management for small businesses — barbershops,
> physiotherapists, studios) is intentionally simple. **The architecture is the star.**
>
> _"Termin" is German for appointment — the target market is Germany._

<!-- Phase 0: vision + architecture-at-a-glance skeleton. The "Architecture at a glance"
     section is finalized in Slice 5 once ADR-001..004 are Accepted. -->

## Why this repo exists

- Demonstrate **senior/architect-level decision making** — every significant choice is an
  [Architecture Decision Record](./docs/adr/README.md).
- Each phase ends in a **demonstrable, working state**: `main` is always green, README always current.
- Produce reusable content for planned deep-dive articles (Keycloak, SSE vs. WebSocket, OpenTelemetry).

## Architecture at a glance

> 🚧 _Placeholder — finalized in Phase 0 Slice 5 with a context diagram once the founding
> ADRs are accepted._ The system starts as a **modular monolith** whose module boundaries
> are designed as **future service boundaries** (modules expose only public contracts and
> events, own their DB schema, never reference another module's internals — enforced by
> architecture tests). Phase 6 extracts services along those seams.

## Tech stack

| Area | Choice |
|---|---|
| Backend | .NET 10 (LTS), Clean Architecture, custom CQRS (no MediatR — see ADR-004), DDD |
| Validation | FluentValidation as a pipeline behavior in the custom CQRS dispatcher |
| Frontend | Angular 22 (zoneless, OnPush), standalone components, Signal Forms |
| Frontend state | NgRx SignalStore (default); one module in classic NgRx as a documented comparison |
| UI / CSS | Tailwind CSS v4 + Angular Material |
| Identity | Keycloak (tenancy per ADR-002) |
| Data | PostgreSQL (EF Core + Npgsql) — tenancy per ADR-001 |
| Cache | Redis (cache-aside, tenant-prefixed keys) |
| Messaging | RabbitMQ + Outbox/Inbox (client per ADR-003) |
| Search | Elasticsearch (tenant isolation per ADR-007) |
| AI | Standalone service — Microsoft Agent Framework + Azure AI Foundry, tool calling, SSE |
| Background jobs | Custom `BackgroundService` (outbox) + Quartz.NET (scheduling) |
| Observability | OpenTelemetry (logs/metrics/traces) → Grafana stack locally |
| Testing | xUnit, ArchUnitNET, Testcontainers (backend); Vitest, Playwright (frontend) |
| IaC / Deploy | Terraform → Azure, GitHub Actions |

## Repository layout

```
src/            backend projects (Clean Architecture — populated in Phase 1)
tests/          backend tests (unit, architecture, integration)
frontend/       Angular 22 app (Phase 1)
docs/adr/       Architecture Decision Records (MADR)
docs/diagrams/  architecture diagrams
docker-compose.yml   local dev stack (grows phase by phase)
```

## Roadmap

Full plan in [`ROADMAP.md`](./ROADMAP.md). Progress:

| Phase | Focus | Status |
|---|---|---|
| 0 | Foundation + ADRs | 🚧 in progress |
| 1 | Modular monolith MVP (Booking) | ⬜ |
| 2 | Multi-tenancy + Keycloak + Redis | ⬜ |
| 3 | RabbitMQ + Outbox/Inbox | ⬜ |
| 4 | Elasticsearch + OpenTelemetry | ⬜ |
| 4.5 | AI booking assistant | ⬜ |
| 5 | Terraform + Azure + content | ⬜ |
| 6 | Microservice extraction + YARP gateway | ⬜ |

## License

[MIT](./LICENSE)
