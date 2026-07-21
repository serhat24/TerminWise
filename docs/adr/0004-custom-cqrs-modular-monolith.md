# ADR-004: Custom CQRS and modular-monolith-first architecture

- **Status:** Accepted
- **Date:** 2026-07-21
- **Deciders:** Serhat Alaftekin
- **Phase:** 0 (implemented from Phase 1; extraction in Phase 6)

## Context and Problem Statement

Two foundational shape-of-the-code decisions must be made before Phase 1:

1. **In-process dispatch.** Command/query handlers need a dispatcher and a pipeline for
   cross-cutting concerns (validation via FluentValidation, logging). The default reflex is
   MediatR. Is that the right dependency for this repo?
2. **Deployment topology over time.** Do we start distributed (microservices), or as a
   monolith? And if a monolith, how do we avoid it rotting into a big ball of mud and keep
   Phase 6 service extraction cheap and real?

This is a **reference repository meant to be read and freely adopted**; dependency weight,
licensing, and the *legibility* of core patterns are first-class concerns.

## Decision Drivers

- **Teach the pattern, don't hide it** — the mediator/pipeline and module-boundary patterns are part of what the repo demonstrates.
- **Licensing & adoptability** — stay freely usable by corporate adopters; avoid paid or reciprocal-copyleft dependencies on the core path.
- **Dependency-light** — fewer heavy dependencies at the center of the architecture.
- **Boundary correctness** — get module seams right *before* paying distribution's cost; make Phase 6 extraction near-zero-change (proof the boundary was real).
- **Operational simplicity early** — one deployable, one database, in-process events while the domain is still being learned.
- **Enforceability** — boundaries and dispatch conventions must be *executable law* (architecture tests), not documentation.

## Considered Options

**Dimension A — In-process dispatcher**

1. **Custom CQRS dispatcher** — own `ICommand`/`IQuery` + handler interfaces, DI-resolved dispatch, pipeline behaviors.
2. **MediatR** — the de-facto .NET mediator library.
3. **Source-generated OSS mediator** — e.g. `martinothamar/Mediator` (MIT, source generators).

**Dimension B — Deployment topology over time**

4. **Modular monolith first, microservices by extraction** — one deployable with enforced internal module boundaries designed as future service seams.
5. **Microservices from day one** — start distributed.
6. **Traditional layered monolith** — layers, but no enforced module boundaries.

## Decision Outcome

**Chosen: "Custom CQRS dispatcher" (A) + "Modular monolith first, microservices by extraction" (B).**

**Custom dispatcher.** A small, hand-written dispatcher resolves exactly one handler per
command/query via DI, wrapped by **pipeline behaviors** (FluentValidation-based validation,
logging; the Result-pattern mapping per [ADR-011](./0011-error-handling.md)). This mirrors
the ethos of [ADR-003](./0003-message-broker-and-client.md): the repo *demonstrates* the
mediator/pipeline pattern from first principles rather than importing it as a black box. It
also sidesteps a licensing problem — **MediatR v13+ is dual-licensed RPL-1.5 (reciprocal) /
commercial**, free only under a Community tier bounded by revenue/capital thresholds — which
is precisely the friction a dependency-light, corporate-adoptable MIT reference repo should
avoid on its most central abstraction. For adopters who prefer a library, **`martinothamar/Mediator`
(MIT, source-generated)** is the recommended off-ramp — the mediator analogue of Rebus in ADR-003.

**Modular monolith first.** We ship **one deployable** whose internal **modules** (starting
with Booking) are designed as **future service boundaries** from day one: each module owns
its database schema exclusively, exposes only **public contracts and events**, and never
references another module's internals. These rules are **enforced by ArchUnitNET** as
executable law (Domain references nothing; Application references only Domain; Infrastructure
is unreferenced by Domain/Application; exactly one handler per command; no cross-module
internal references).

**Cross-module interaction rules** make "public contracts and events" precise:

- **Async by default:** cross-module flows go through **events** (raised in-process now, carried over the broker from Phase 3), not direct calls.
- **Sync only through a public contract:** a synchronous in-process call is allowed **only** via another module's **public contract interface**, and **only** where the use case genuinely needs immediate consistency — **never** by reaching into another module's database (no cross-module joins or queries; ArchUnitNET catches the reference, and the rule holds in words too).
- **No shared transactions:** modules **never share a database transaction**; consistency *between* modules is **eventual, via the outbox**. This is precisely what makes the Phase 6 extraction "a transport change, not a redesign" — there is no distributed transaction to unwind when a module moves across the network.

Services are **extracted in Phase 6** ([ADR-013](./0013-service-extraction.md))
when a real driver appears (independent scaling/deploy cadence) — at which point a good
boundary makes extraction near-zero-change. The **AI service is the deliberate exception**:
it is standalone from Phase 4.5 because its scaling profile is known up front — *designed*
separate, whereas Notifications is *extracted* once its boundary has matured in-process (the
full designed-vs-extracted contrast lives in [ADR-013](./0013-service-extraction.md)).

### Consequences

- **Good — fully OSS, dependency-light core.** No license keys, no reciprocal-license obligations, nothing revenue-gated on the central dispatch path; the pattern is in the repo to read.
- **Good — boundaries proven before distribution.** Moving a boundary in-process is a refactor; moving it across services is a project. Discovering seams in the monolith first, then extracting, is cheaper and lower-risk than starting distributed and discovering them wrong (Fowler's "monolith first").
- **Good — extraction is measurable evidence.** Phase 6's Booking-module diff for extracting Notifications should be minimal; that minimal diff is the *proof* the boundary was real, documented in ADR-013.
- **Good — right-sized early ops.** One process, one database, in-process domain events while the domain model is still being learned.
- **Trade-off — the dispatcher is ours to maintain.** It is small and bounded (dispatch + pipeline behaviors); advanced MediatR features (notification polymorphism, streaming) are **non-goals** unless a concrete need appears. One-handler-per-command and handler/validator naming are enforced by architecture tests so the "simple" dispatcher can't silently accrete complexity.
- **Trade-off — modular monolith demands enforced discipline.** Without ArchUnitNET the boundaries are just convention and the monolith rots. The architecture-test suite is therefore not optional polish — it is load-bearing, runs in CI from Phase 1, and must fail demonstrably when a layer/module rule is violated (a Phase 1 acceptance criterion).
- **Trade-off — in-process now, async later.** Domain events are raised in-process in Phase 1 but module contracts are **events from the start**, so the Phase 3 move to the broker ([ADR-003](./0003-message-broker-and-client.md)/[ADR-006](./0006-reliable-messaging.md)) is a transport change, not a redesign. Handlers must not assume synchronous in-process delivery forever.
- **Trade-off — deferred distribution costs are deferred, not avoided.** Distributed debugging, contract versioning, and operational complexity arrive in Phase 6; we accept paying them then, in exchange for not paying them before the boundaries are trustworthy.
- **Follow-up (Phase 1):** implement the dispatcher + pipeline behaviors (validation/logging), FluentValidation integration, the Result pattern ([ADR-011](./0011-error-handling.md)), and the ArchUnitNET rule suite. **(Phase 6):** service extraction ([ADR-013](./0013-service-extraction.md)).

## Pros and Cons of the Options

### A1. Custom CQRS dispatcher (chosen)

- 👍 Fully OSS, dependency-light; no licensing considerations on the core path.
- 👍 Demonstrates the mediator/pipeline pattern from first principles — a repo goal.
- 👍 Total control over pipeline ordering, Result mapping, and validation integration.
- 👎 Our code to write, test, and maintain (bounded, but non-zero).
- 👎 No ecosystem of ready-made behaviors; advanced features must be built if ever needed.

### A2. MediatR

- 👍 De-facto standard, well-documented, large ecosystem; batteries-included pipeline.
- 👎 **v13+ is dual RPL-1.5 (reciprocal) / commercial**; free only under a revenue/capital-bounded Community tier — friction for corporate adopters and counter to the dependency-light goal.
- 👎 Hides exactly the pattern the repo intends to teach.

### A3. Source-generated OSS mediator (`martinothamar/Mediator`)

- 👍 **MIT-licensed**, actively maintained; source generators → high performance and Native AOT; MediatR-like API.
- 👍 A genuine free off-ramp for adopters who want a library rather than hand-rolled dispatch.
- 👎 Still a dependency that abstracts the pattern away (fine for a product, less so for a teaching repo).
- 👎 Source-generator coupling and its own learning curve.

### B4. Modular monolith first, extract later (chosen)

- 👍 Cheapest place to get boundaries right; extraction becomes near-zero-change when they are.
- 👍 Operational simplicity early; single deployable and database.
- 👍 Boundaries enforced as executable law (ArchUnitNET), not convention.
- 👎 Requires discipline + enforcement or it rots; distribution costs merely deferred to Phase 6.

### B5. Microservices from day one

- 👍 Independent scaling/deploy immediately; hard physical boundaries.
- 👎 Pays full distributed cost (ops, debugging, contract versioning) *before* boundaries are validated — high risk of cutting them in the wrong place and paying to move them across the network.
- 👎 Overkill for an early-stage domain still being learned; obscures the "extraction proves the boundary" narrative.

### B6. Traditional layered monolith

- 👍 Simple and familiar.
- 👎 No enforced module boundaries → tends toward a big ball of mud; future extraction is expensive archaeology.
- 👎 Misses the entire point of demonstrating clean, extractable module seams.

## More Information

Sources consulted (verified 2026-07-21):

- **MediatR licensing** — from **v13.0**, MediatR ships on NuGet under a **dual RPL-1.5 / commercial** license; a **Community edition is free** only for orgs/individuals under **$5M gross annual revenue** and **$10M outside capital** (plus non-profit/educational/non-production use), otherwise a paid commercial tier by team size. Older versions remain under the previous permissive license. See [Jimmy Bogard — AutoMapper & MediatR Commercial Editions](https://www.jimmybogard.com/automapper-and-mediatr-commercial-editions-launch-today/) and the [Lucky Penny Software Licensing FAQ](https://luckypennysoftware.com/faq).
- **`martinothamar/Mediator`** — MIT-licensed, source-generated, Native-AOT-capable MediatR alternative, actively maintained: [GitHub](https://github.com/martinothamar/Mediator).
- **Monolith-first / extraction** — the boundary-correctness rationale follows the well-known "MonolithFirst" guidance (extract services once boundaries are understood, not before).

Related: [ADR-003](./0003-message-broker-and-client.md) (same first-principles/licensing ethos; module events become broker messages), [ADR-011](./0011-error-handling.md) (Result pattern mapped in the pipeline), [ADR-013](./0013-service-extraction.md) (Phase 6 extraction — where boundary quality is measured).
