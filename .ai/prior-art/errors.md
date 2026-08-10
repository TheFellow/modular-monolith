# Typed application errors

Status: Accepted
Date: 2026-08-09

## Context

The Go reference deliberately routes domain, persistence, middleware, and
presentation failures through `pkg/errors`. Its six stable kinds carry safe
presentation text and CLI, HTTP, gRPC, and terminal metadata while retaining a
wrapped diagnostic cause. That contract is architecture, not a Go artifact:
authorization denials, rollback, failed-attempt audit, page elision, and every
surface depend on reliable classification through wrapping.

## Candidates

| Candidate | Strength | Material mismatch | Result |
| --- | --- | --- | --- |
| Owned `AppError` exception family | Native stack propagation, typed catches, inner causes, no dependency | Must maintain catalog and adapters | Choose |
| ErrorOr | Explicit success/error return values and built-in error categories | Changes every operation signature and does not preserve the reference exception pipeline | Reject |
| OneOf | Closed compile-time unions per operation | Repeats failure alternatives on every signature and has no shared transport/safety contract | Reject |
| FluentResults | Rich result composition and metadata | Adds a parallel result object model and surface-specific extensions | Reject |
| Plain framework exceptions | Idiomatic for programmer/runtime faults | Cannot express the stable application taxonomy or safe-message policy | Reject |
| ASP.NET Core `ProblemDetails` | Standard HTTP representation | An edge DTO, not a domain or non-HTTP error model | Use later as an adapter only |

## Decision

Semantically port `pkg/errors` as an owned `Mixology.Kernel.Errors` family.
`InvalidError`, `NotFoundError`, `PermissionError`, `ConflictError`,
`FailedPreconditionError`, and `InternalError` derive from `AppError`. An
immutable catalog maps each kind to its default message, HTTP status, gRPC
status, CLI exit code, and terminal style. Lower layers throw only typed
application errors they understand; unknown dependency failures are wrapped as
`InternalError` at an owning boundary.

The exception detail and inner exception are diagnostic. `UserMessage` exposes
actionable detail for non-internal errors but hides internal detail unless code
provides an explicit safe override. Surface adapters must render
`UserMessage`, never raw unknown exception text. HTTP may translate the same
catalog to `ProblemDetails`; gRPC and terminal surfaces get equally thin
adapters without references from the kernel.

Classification must traverse ordinary inner exceptions and every branch of an
`AggregateException`, mirroring Go's `errors.Is`/`errors.As` behavior for
wrapped and joined causes. Cancellation is detected independently and before
fallback or internal wrapping. Persistence, Cedar, dispatch, middleware, and
domain recovery code use `AppError.Find`/`Is*`; outer-type-only matching is a
bug because it loses meaning once a layer adds context.

Use ordinary framework argument exceptions for public API programmer errors
where appropriate. Application validation belongs to `InvalidError` so it has
the reference transport contract. A result type remains reasonable for a
future hot-path parser or evaluator, but it must translate into this taxonomy
at the application boundary rather than establish a second error vocabulary.

The six concrete types are intentionally readable teaching code. Their catalog
and parity tests are the source of truth; introduce generation only if the
family grows enough that handwritten synchronization becomes a demonstrated
maintenance problem.

## Validation gates

- Assert every kind's default, HTTP, gRPC, CLI, and terminal mapping against the
  Go contract.
- Assert concrete runtime types as well as kind-based matching.
- Assert wrapped and aggregate-branch lookup, including a typed cause below an
  infrastructure wrapper.
- Assert cancellation is never converted to NotFound, a fallback value, or an
  internal application failure.
- Assert internal and unknown failures never reveal diagnostic detail at CLI,
  TUI, GUI, HTTP, or gRPC edges.
- Assert command rollback and failed-attempt audit preserve the original typed
  classification.
- Reject direct presentation decisions in domain and persistence assemblies
  with architecture tests.

## Sources

- [Go reference `pkg/errors`](https://github.com/TheFellow/go-modular-monolith/tree/main/pkg/errors)
- [.NET exception best practices](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions)
- [C# exception propagation](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/exceptions/using-exceptions)
- [ASP.NET Core ProblemDetails](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.problemdetails?view=aspnetcore-10.0)
- [ErrorOr](https://github.com/amantinband/error-or)
- [OneOf](https://github.com/mcintyre321/OneOf)
- [FluentResults](https://github.com/altmann/FluentResults)
