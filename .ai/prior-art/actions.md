# Action projection

Status: Accepted

Date: 2026-08-09

## Decision

Keep action projection as a small application-owned abstraction built from
immutable C# records and typed asynchronous delegates. Do not bind domain
capabilities to Avalonia commands, Terminal.Gui controls, or another UI toolkit.
Every surface consumes the same stable action ID plus visible/enabled/disabled
reason state, while command handlers remain the authoritative security and
invariant boundary.

The evaluator semantically ports `pkg/presentation/actions`: group permissions
are inherited unless a control or nested group explicitly replaces or removes
them; a typed permission denial hides a control and skips its conditions; the
first unmet prerequisite leaves an authorized control visible but disabled;
and evaluator failures retain the owned `AppError` classification. Cancellation
is never translated into a domain failure.

No third-party capability or MVVM package is used here. Records give the small
snapshot types value equality and immutable-data semantics, while delegates
keep authorization and prerequisite evaluation framework-neutral. Avalonia's
MVVM layer and Terminal.Gui adapters will map these results to native controls
later instead of owning policy decisions.

## Rejected alternatives

- Toolkit-specific command enablement would duplicate authorization and
  prerequisite semantics across the TUI and desktop surface.
- Reflection over module methods would make stable control identity and
  per-action overrides implicit.
- Treating every evaluator exception as denial would hide infrastructure
  failures and could leave stale UI state looking authoritative.

## Validation

Tests cover permission-denied non-disclosure, ordered prerequisite short
circuiting, public and required overrides, duplicate/empty declaration errors,
typed evaluator failures, unknown failure wrapping, and cancellation.

## Sources

- Reference implementation: `/go-modular-monolith/pkg/presentation/actions`
- [C# record types](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records)
- [C# delegates specification](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/delegates)
- [.NET task cancellation](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation)
