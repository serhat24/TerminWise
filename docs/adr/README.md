# Architecture Decision Records

Every significant architectural choice in TerminWise is recorded as an ADR, in
[MADR](https://adr.github.io/madr/) format. This is a deliberate demonstration of
senior/architect-level decision making: **context → options considered → decision →
consequences**, with source-verified facts (no reliance on stale memory for
licensing or version claims).

- Template: [`0000-madr-template.md`](./0000-madr-template.md)
- Format: MADR (Markdown Any Decision Records)
- An ADR is **Accepted** before the code implementing its decision is merged.

## Index

| ADR | Title | Status | Phase |
|---|---|---|---|
| [ADR-001](./0001-postgres-tenancy-model.md) | Tenancy model on PostgreSQL (db-per-tenant vs schema-per-tenant vs shared+discriminator vs shared+RLS) | Accepted | 0 |
| [ADR-002](./0002-keycloak-tenancy.md) | Keycloak tenancy (realm-per-tenant vs single realm + Organizations/groups) | Accepted | 0 |
| [ADR-003](./0003-message-broker-and-client.md) | Message broker & client library (RabbitMQ vs Kafka; thin custom vs MassTransit vs Rebus) | Accepted | 0 |
| [ADR-004](./0004-custom-cqrs-modular-monolith.md) | Custom CQRS + modular-monolith-first (why no MediatR; module boundaries as future services) | Accepted | 0 |
| [ADR-011](./0011-error-handling.md) | Error handling — Result pattern + RFC 9457 mapping (custom vs ErrorOr vs FluentResults) | Proposed | 1 |

> Later phases add ADR-005 (cache), 006 (reliable messaging), 007 (search isolation),
> 008/008b (AI authz & stack), 009 (frontend state), 010 (UI foundation),
> 011 (error handling), 012 (observability), 013 (service extraction), 014 (background jobs).
