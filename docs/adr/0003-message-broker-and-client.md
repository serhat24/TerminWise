# ADR-003: Message broker and client library

- **Status:** Accepted
- **Date:** 2026-07-21
- **Deciders:** Serhat Alaftekin
- **Phase:** 0 (implemented in Phase 3)

## Context and Problem Statement

From Phase 3, TerminWise becomes event-driven: domain events (e.g. `AppointmentCreated`)
flow through an **Outbox** to a broker, then to consumers (Notifications) with
**Inbox/idempotency**, retries, and dead-lettering. We need to choose (1) the **broker**
and (2) the **client library / abstraction** the .NET code uses to talk to it.

Two things constrain the choice. First, the *workload* is classic transactional messaging —
routing, per-message acknowledgment, competing consumers, dead-letter handling — not
high-volume log streaming with replay. Second, this is a **reference repository meant to be
read and freely adopted**, so licensing and dependency weight matter as much as features.

## Decision Drivers

- **Fit to workload** — reliable command/event delivery, flexible routing, competing consumers, DLQ; not stream retention/replay analytics.
- **Licensing & adoptability** — the repo must stay freely usable by corporate adopters; a paid or soon-EOL dependency is a liability.
- **Dependency-light, first-principles ethos** — consistent with the custom-CQRS decision ([ADR-004](./0004-custom-cqrs-modular-monolith.md)): demonstrate the pattern, don't hide it behind a heavy framework.
- **Reliability primitives** — publisher confirms, manual ack, dead-letter exchanges, durable/quorum queues.
- **Operational simplicity** — one broker to run locally (docker-compose) and reason about.
- **Demonstrability & teachability** — the outbox → broker → consumer → inbox path should be visible, not magic.

## Considered Options

**Dimension A — Broker**

1. **RabbitMQ** — classic message broker (exchanges, queues, routing, per-message ack, DLX).
2. **Apache Kafka** — distributed event-streaming log (partitions, retention, consumer-group offsets, replay).

**Dimension B — Client library / abstraction (on the chosen broker)**

3. **Thin custom abstraction over the official `RabbitMQ.Client`** — own `IEventPublisher`/consumer interfaces, topology, retry/DLQ.
4. **MassTransit** — full-featured .NET messaging framework.
5. **Rebus** — lean OSS .NET service bus ("dumb pipes").

## Decision Outcome

**Chosen: "RabbitMQ" (A) + "thin custom abstraction over `RabbitMQ.Client`" (B).**

RabbitMQ matches the workload directly: exchanges/bindings for routing, per-message
acknowledgment, dead-letter exchanges, competing consumers, and **quorum queues** for
reliability — exactly the primitives the Phase 3 outbox/inbox story needs. Kafka's strengths
(high-throughput retention, replayable log, stream processing) solve problems TerminWise
doesn't have, at real operational cost.

For the client, we build a **thin abstraction over the official `RabbitMQ.Client` (v7,
async-first)** rather than adopt a framework. This is driven by two forces that point the
same way:

- **Ethos:** it mirrors [ADR-004](./0004-custom-cqrs-modular-monolith.md) (custom CQRS, no MediatR) — the repo *teaches* messaging patterns (publisher confirms, consumer topology, retry, DLQ, inbox dedup) from first principles instead of hiding them.
- **Licensing:** **MassTransit v9 is commercial (2026)** and **v8 is end-of-life at the end of 2026** (security patches only) — adopting it would build the reference architecture on a dependency that is either paid or expiring within months. The custom path keeps the repo **fully OSS (MPL-2.0/Apache-2.0) and dependency-light**, which is a core goal.

**Rebus (MIT)** is explicitly documented as the pragmatic OSS **framework alternative** for
adopters who would rather not hand-roll — it is the recommended off-ramp, not MassTransit.

**Scope of the custom client (explicit non-goals).** To keep Phase 3 from quietly growing
into a framework, the abstraction's surface is fixed:

- **In scope:** publish; consume (competing consumers); retry with backoff; dead-letter (DLQ) topology; inbox deduplication; message-header propagation.
- **Out of scope (non-goals):** sagas / process managers; scheduling (Quartz.NET owns that — ADR-014); a multi-broker transport abstraction (we target RabbitMQ only — the seam that keeps `RabbitMQ.Client` from leaking across modules exists for testability and boundary hygiene, not for portability).

**Message envelope contract.** Two headers are part of the envelope from day one, designed
in rather than bolted on:

- **Tenant propagation:** every message carries `tenant_id` in its envelope; a consumer sets `app.current_tenant` from it *before* any database work — extending the ADR-001/ADR-002 through-line (`org claim → app.current_tenant → RLS`) into the asynchronous path. Consumers **fail-closed on a missing/empty tenant header**: the message is dead-lettered, never processed unscoped.
- **Trace propagation:** the client propagates **W3C trace context** (`traceparent`) through message headers, so a published event and its consumer join the same distributed trace — designing in Phase 4's "a single trace spans API → broker → consumer" acceptance criterion instead of retrofitting it.

### Consequences

- **Good — fully OSS, no licensing cliff.** The stack depends only on `RabbitMQ.Client` (MPL-2.0/Apache-2.0); nothing to pay for, nothing expiring at end of 2026.
- **Good — the interesting parts are visible.** Outbox dispatch, publisher confirms, competing consumers, retry with backoff, dead-lettering, and inbox idempotency are implemented and readable — the teaching goal of the repo, and the raw material for the planned articles.
- **Good — right-sized ops.** One RabbitMQ container locally; quorum queues give durability without a ZooKeeper/KRaft-class cluster.
- **Trade-off — we build what a framework would give us.** Retry policies, DLQ topology, serialization, consumer concurrency, and outbox/inbox are ours to write and test. We deliberately reimplement a *focused subset* of MassTransit/Rebus, not a general framework; scope is bounded to what Phases 3/6 actually need, and covered by integration tests (Testcontainers RabbitMQ).
- **Trade-off — no free advanced features.** Saga/state-machine orchestration, scheduling, and a large transport catalog (that MassTransit/Rebus offer) are out of scope; if a real saga need appears later, revisit with Rebus rather than re-evaluating MassTransit's licensing.
- **Trade-off — `RabbitMQ.Client` v7 is async-first** (breaking vs v6); the abstraction targets the async API from the start.
- **Follow-up (Phase 3):** the reliability semantics (outbox/inbox, idempotency keys, poison-message/DLQ handling, at-least-once + dedup) are specified in **[ADR-006](./0006-reliable-messaging.md)**; the outbox dispatcher runs as a custom `BackgroundService` per **ADR-014**. The abstraction must not leak `RabbitMQ.Client` types across module boundaries (architecture-test enforced), keeping a future transport swap feasible.

## Pros and Cons of the Options

### A1. RabbitMQ (chosen broker)

- 👍 Purpose-built for routing + reliable delivery: exchanges/bindings, per-message ack, DLX, competing consumers, quorum queues.
- 👍 Simple to run and reason about locally; mature .NET client.
- 👍 Matches the outbox/inbox + reminders workload precisely.
- 👎 Not built for massive-throughput retention/replay or log analytics.
- 👎 Message replay/audit requires deliberate design (it isn't a retained log).

### A2. Apache Kafka

- 👍 Excellent for high-volume streaming, durable retained log, replay, and stream processing.
- 👍 Consumer-group offset model scales horizontally.
- 👎 Overkill for transactional booking/notification messaging; heavier ops (brokers, partitions, KRaft).
- 👎 Classic patterns here (per-message DLQ, selective routing, competing consumers) are more awkward than in RabbitMQ.
- 👎 Its headline strengths (retention/replay) aren't requirements for this domain.

### B3. Thin custom abstraction over `RabbitMQ.Client` (chosen client)

- 👍 Fully OSS, dependency-light; no licensing risk.
- 👍 Demonstrates messaging patterns from first principles — the repo's purpose.
- 👍 Total control over topology, retries, DLQ, and the outbox/inbox seams.
- 👎 More code to write, test, and maintain; reliability primitives are our responsibility.
- 👎 No sagas/scheduling/transport catalog out of the box.

### B4. MassTransit

- 👍 Batteries-included: sagas, scheduling, retry/DLQ, many transports, large ecosystem.
- 👎 **v9 is commercial (2026); v8 is EOL end-of-2026 (security patches only)** — a paid-or-expiring dependency at the heart of a free reference repo.
- 👎 Abstracts away exactly the patterns this repo intends to teach.

### B5. Rebus

- 👍 **MIT-licensed, actively maintained** (8.9.2, 2026); lean "dumb pipes" design; much lighter than MassTransit.
- 👍 A genuine OSS framework off-ramp for adopters who don't want to hand-roll.
- 👎 Still a framework dependency that hides the primitives we want to demonstrate.
- 👎 Adopting it would make the messaging code framework-shaped rather than first-principles — counter to the repo's ethos (but a reasonable choice for a real product).

## More Information

Sources consulted (verified 2026-07-21):

- **MassTransit licensing** — v9 released under a **commercial** license (2026); **v8 remains OSS but reaches end-of-life at the end of 2026** (security patches through 2026, then unsupported). See the maintainer/community coverage: [Milan Jovanović — "MediatR and MassTransit Going Commercial"](https://milanjovanovic.tech/blog/mediatr-and-masstransit-going-commercial-what-this-means-for-you); [MassTransit commercial license](https://massient.com/license).
- **Rebus** — MIT-licensed, latest **8.9.2** (Apr 2026), active: [NuGet](https://www.nuget.org/packages/Rebus), [GitHub](https://github.com/rebus-org/Rebus).
- **RabbitMQ .NET client** — official `RabbitMQ.Client` **7.2.1**, dual-licensed **MPL-2.0 / Apache-2.0**, async-first: [GitHub](https://github.com/rabbitmq/rabbitmq-dotnet-client), [NuGet](https://www.nuget.org/packages/RabbitMQ.Client/).

Related: [ADR-004](./0004-custom-cqrs-modular-monolith.md) (same dependency-light, first-principles ethos), [ADR-006](./0006-reliable-messaging.md) (outbox/inbox/idempotency semantics — Phase 3), ADR-014 (background-job strategy — outbox dispatcher).
