# ADR-011: Error handling — Result pattern and RFC 9457 mapping

- **Status:** Proposed
- **Date:** 2026-07-21
- **Deciders:** Serhat Alaftekin
- **Phase:** 1

## Context and Problem Statement

TerminWise's command/query handlers ([ADR-004](./0004-custom-cqrs-modular-monolith.md)) must
report *expected* failures — a validation error, a missing entity, a business-rule violation
like **"that time slot is already taken"** — in a way that is consistent, testable, and cheap.

Two questions: (1) how do handlers *represent* an expected failure — a thrown exception, or a
returned value? (2) how does that failure become a consistent HTTP error the Angular client
can act on? And underneath both: **where is the line between an expected failure and a bug?**

## Decision Drivers

- **Expected failures are not exceptional** — booking a taken slot is normal operation; using exceptions for it is costly (stack captures) and hides intent.
- **Consistency at the edge** — every failure should surface as one predictable, machine-readable HTTP shape.
- **Client actionability** — the Angular client needs a *stable* key to branch on and localize, not a human sentence it must string-match.
- **Ethos & control** ([ADR-004](./0004-custom-cqrs-modular-monolith.md)) — own the error taxonomy and its HTTP mapping; keep the core dependency-light and legible.
- **Testability** — asserting `result.Error == BookingErrors.SlotAlreadyTaken` in a unit test should need no HTTP host.
- **Standards alignment** — errors should follow the current problem-details standard.

## Considered Options

1. **Custom lightweight `Result` / `Result<T>` + `Error`** (with an `ErrorType` taxonomy), handlers return Results, one mapping layer → RFC 9457. **← chosen**
2. **ErrorOr** (MIT) — discriminated union `ErrorOr<T>` with a built-in error-type taxonomy.
3. **FluentResults** (MIT) — richer `Result`/`Result<T>` with error accumulation and cause chains.
4. **Exceptions for control flow** — throw typed exceptions for expected failures, translate in middleware.

## Decision Outcome

**Chosen: "1 — custom `Result`/`Result<T>` + `Error`," with a single Result→RFC 9457 mapping layer.**

Handlers return `Result` / `Result<T>`; an expected failure is `Result.Failure(Error)`, never a
thrown exception. An `Error` is a small record — a stable **code**, a human **description**, and
an **`ErrorType`** — and a single mapping layer at the API edge turns any `Result` (and any
FluentValidation failure) into an **RFC 9457 problem-details** response via .NET's
`IProblemDetailsService`.

> **Query filter vs. RLS had "ergonomics vs. guarantee"; here the analogue is: the `Result` is the
> contract, the mapping layer is the single translation.** Handlers speak one language (Results);
> exactly one place knows how a failure becomes HTTP.

**Honesty about this choice.** Unlike [ADR-003](./0003-message-broker-and-client.md)/[ADR-004](./0004-custom-cqrs-modular-monolith.md),
licensing does *not* force this: **ErrorOr and FluentResults are both MIT and genuinely good.** Custom
wins here only on **consistency with the custom-CQRS ethos, full control of the taxonomy→HTTP mapping,
and dependency-lightness** — the types involved are trivial to own. For adopters who would rather take a
library, **ErrorOr is the recommended off-ramp** (DDD-aligned, near-identical taxonomy).

### The boundary: Results vs. exceptions

**Results for expected failures; exceptions for bugs and infrastructure.** The line:

> If a well-behaved client could plausibly trigger it and deserves a meaningful, actionable
> response, it is a **Result**. If it means the code or its environment is broken — a violated
> invariant that "can't happen," a null bug, the database being down — it is an **exception**:
> unhandled, logged, alerted, and surfaced as a generic `500` (no internal detail leaked).

FluentValidation failures are Results (→ `400`). Infrastructure exceptions bubble to the
`UseExceptionHandler` middleware, which emits a `500` problem-details via the same service — so even
uncaught faults share the response shape, without the domain ever throwing for control flow.

### Error taxonomy → HTTP status

| `ErrorType` | HTTP | RFC 9457 use | Example |
|---|---|---|---|
| `Validation` | **400** | `ValidationProblemDetails` (per-field `errors`) | "startsAt is required" (FluentValidation) |
| `NotFound` | **404** | problem details | business id doesn't exist |
| `Conflict` | **409** | problem details | **"slot already taken"** |
| `Forbidden` | **403** | problem details | authenticated but not allowed |
| `Unauthorized` | **401** | problem details | missing/invalid token |
| `Unexpected` | **500** | problem details (via exception handler) | *should be an exception, not a Result* |

The mapper owns this table — the single source of truth for status codes.

### The full journey of one failure — "slot already taken"

1. **Domain** — the availability rule finds the slot taken and returns a failure, not an exception:
   ```csharp
   // BookingErrors.cs
   public static readonly Error SlotAlreadyTaken =
       Error.Conflict("Booking.SlotAlreadyTaken", "The selected time slot is no longer available.");

   // in the Appointment aggregate / availability service
   if (slotTaken) return Result.Failure<AppointmentId>(BookingErrors.SlotAlreadyTaken);
   ```
2. **Handler** — `BookAppointmentHandler` returns `Result<AppointmentId>` carrying that `Error`. No `try/catch`, no throw.
3. **Mapping layer** — the endpoint sees a failed `Result`, reads `Error.Type == Conflict`, and emits **HTTP 409** as RFC 9457 problem details (`application/problem+json`):
   ```json
   {
     "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
     "title": "Conflict",
     "status": 409,
     "detail": "The selected time slot is no longer available.",
     "instance": "/api/appointments",
     "errorCode": "Booking.SlotAlreadyTaken",
     "traceId": "00-4bf92f...-01"
   }
   ```
   `errorCode` (stable) and `traceId` are RFC 9457 **extension members**; `traceId` ties into observability ([ADR-012](./0012-observability.md)).
4. **Angular client** — `httpResource`/`HttpClient` receives the 409; a typed error interceptor branches on the **`errorCode`** (`Booking.SlotAlreadyTaken`), not the human `detail`, and shows a localized message ("That slot was just taken — pick another"), surfaced on the Signal Form. The stable code is the contract; `detail` is for humans/logs.

### Consequences

- **Good — expected failures are cheap and explicit.** No stack-capture cost on normal paths; a handler's signature (`Result<T>`) advertises that it can fail.
- **Good — one consistent error shape.** Every failure — domain Result, validation, or uncaught exception — becomes RFC 9457 problem details through one service; the client learns one format.
- **Good — stable client contract.** Clients branch on `errorCode`, decoupled from wording/localization; the taxonomy→status table lives in exactly one mapper.
- **Good — trivially testable.** Domain/handler tests assert on `Result.Error` with no HTTP host; the mapping is tested once.
- **Trade-off — honest closeness.** ErrorOr/FluentResults are free and capable; choosing custom is an ethos/control decision, not a necessity. Documented so the reasoning isn't mistaken for a licensing forced-move. Off-ramp: **ErrorOr**.
- **Trade-off — discipline required.** Handlers must *return* expected failures, not throw. Enforced by review and an architecture test (handlers return `Result`/`Result<T>`); a `Match`/`Switch` API steers call sites toward handling both branches (C# has no exhaustive discriminated union to force it).
- **Trade-off — keep it small.** The Result/Error types + mapper must not accrete into a framework; scope is fixed at `Result`, `Result<T>`, `Error`, `ErrorType`, and one mapper.
- **Follow-up (Phase 1):** implement the types + the FluentValidation pipeline behavior (validation failures → `Result`/400) + the single `IProblemDetailsService` mapper; register `AddProblemDetails()` + `UseExceptionHandler()`. Wire the `traceId` extension member (ADR-012). Consider a curated `type` URI catalog later; `errorCode` is the stable key until then.

## Pros and Cons of the Options

### 1. Custom `Result`/`Result<T>` + `Error` (chosen)

- 👍 Full control of the taxonomy and its HTTP/9457 mapping; dependency-light; consistent with custom CQRS.
- 👍 Cheap on the happy-and-expected-failure paths; self-documenting handler signatures.
- 👎 Ours to build and keep small; C# lacks true discriminated unions, so exhaustiveness is by convention/analyzer, not the compiler.

### 2. ErrorOr (MIT)

- 👍 **MIT**, DDD-aligned, ASP.NET-friendly; built-in error types (Validation/NotFound/Conflict/Forbidden/…); railway `Then`/`ThenAsync`.
- 👍 The recommended off-ramp — minimal conceptual distance from the custom design.
- 👎 A dependency that hides the pattern this repo means to demonstrate.

### 3. FluentResults (MIT)

- 👍 **MIT**, mature; error **accumulation** and rich cause chains/metadata — strong for bulk/import scenarios.
- 👎 Heavier model than needed; no opinionated HTTP-status taxonomy (we'd still write the mapping).

### 4. Exceptions for control flow

- 👍 Familiar; centralized translation in middleware.
- 👎 Costly for normal failures; hides that a method can fail; blurs the very bug-vs-expected line this ADR draws. Rejected as the baseline anti-pattern.

## More Information

Sources consulted (verified 2026-07-21):

- **RFC 9457 — Problem Details for HTTP APIs** (July 2023), which **obsoletes RFC 7807**: [rfc-editor.org/rfc/rfc9457](https://www.rfc-editor.org/rfc/rfc9457.html). *Note: the ROADMAP references "RFC 7807"; we implement its successor, 9457.*
- **ASP.NET Core problem details (.NET 10)** — `AddProblemDetails`, `IProblemDetailsService`, `ProblemDetails`/`ValidationProblemDetails`, `ProblemDetailsOptions.CustomizeProblemDetails`, extension members, `UseExceptionHandler`: [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0) (now cites RFC 9457). Minimal API validation integrates with `IProblemDetailsService` in .NET 10.
- **ErrorOr** — MIT, v2.1.1: [GitHub](https://github.com/amantinband/error-or) / [NuGet](https://www.nuget.org/packages/ErrorOr). **FluentResults** — MIT, v4.0.0: [GitHub](https://github.com/altmann/FluentResults) / [NuGet](https://www.nuget.org/packages/FluentResults).

Related: [ADR-004](./0004-custom-cqrs-modular-monolith.md) (handlers return Results; validation is a pipeline behavior), [ADR-012](./0012-observability.md) (`traceId` extension member), and FluentValidation wiring (Phase 1 scope).
