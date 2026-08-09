# Dashboard and navigation projection

Status: Accepted

Date: 2026-08-09

## Inherited requirements

The Go application loads each dashboard count independently, leaves denied or
failed values at `-1`, retains the first non-permission error, and keeps recent
audit activity in audit query order. Top-level navigation consumes the same
domain capability projections as detail surfaces: denial hides a destination,
while an evaluator outage keeps it visible so the destination can report the
problem.

## Decision

Add a toolkit-free `Mixology.Presentation` assembly above Application and the
bounded contexts. Owner modules continue to own Cedar action projection. The
new assembly composes only public module APIs into immutable dashboard and
navigation snapshots; CLI, TUI, and GUI may depend on it, while it cannot
depend on any executable or UI toolkit.

Dashboard data access is represented by a narrow `IDashboardDataSource`. The
production adapter binds the current `MixologySession` to public module
`CountAsync` and Audit `ListAsync` calls. This makes independent failure and
permission degradation explicit and testable without mocking modules or EF.
Cancellation is propagated immediately. A result carries both partial data and
the first non-permission error so future view models can render partial state;
the CLI preserves Go behavior by returning the error before writing output.

Audit actors use canonical Cedar UID text, not a role shorthand. Recent
activity is capped at ten items and preserves the module's descending order.
The inventory low-stock aggregate uses the owner-defined inclusive default
threshold.

## Rejected alternatives

- Putting dashboard composition in `Mixology.Application` would reverse the
  dependency direction because Application is below every bounded context.
- Putting it in CLI or a future GUI would duplicate aggregation and
  authorization semantics across surfaces.
- Running aggregates as one all-or-nothing query would discard useful partial
  state and diverge from the reference behavior.
- Hiding navigation on every error would make an authorization outage
  indistinguishable from a real denial.

## Validation

Tests pin independent aggregate execution order, permission degradation,
first-error retention, cancellation, lifecycle projectors, authorization-aware
navigation, text/JSON CLI output, `--actor|--as`, and canonical audit actors.
Architecture tests require Presentation to remain toolkit-free, reference only
Application and modules, and never become a module dependency.

## Sources

- Reference dashboard: `/go-modular-monolith/app/dashboard.go`
- Reference navigation: `/go-modular-monolith/main/gui/desktop.go`
- Reference CLI status: `/go-modular-monolith/main/cli/dashboard.go`
- [Dependency injection guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
- [.NET cancellation model](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)
