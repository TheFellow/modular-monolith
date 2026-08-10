# Test utilities and project map

The .NET port uses production-shaped helpers local to each test project rather
than one public `testutil` package. Shared infrastructure is deliberately small:
xUnit supplies assertions/lifecycle, temporary directories isolate SQLite, and
surface constructors accept streams, dispatchers, runtimes, or controlled
asynchronous delegates.

## Test layers

- `Mixology.Kernel.Tests`, `Filtering.Tests`, and other foundation projects test
  value contracts and algorithms directly.
- `Mixology.Modules.*.Tests` compose real persistence, authorization, pipeline,
  event dispatch, and audit behavior for one bounded context.
- `Mixology.Cli.Tests`, `Tui.Tests`, and `Desktop.Tests` exercise native parser,
  workspace/view-model, and real control adapters respectively.
- `Mixology.Architecture.Tests` enforce project/namespace direction and public
  facade boundaries.
- `Mixology.Dispatcher.Tests` verify generated routes, ordering, fresh handler
  resolution, and reciprocal workflow consistency.

## Application fixtures

Create a unique database beneath the test's temporary directory, build the same
Generic Host registrations as production, run real migrations, seed only the
minimum public models required by the scenario, and dispose the host. Use
`TimeProvider` and injected actor/session factories for deterministic time and
authorization; do not bypass a public module request merely to shorten setup.

Assertions should classify failures with `AppError.Is*` or match the concrete
error type, then separately assert the safe user message when testing a
presentation boundary. Audit tests query the latest entry/touches through the
Audit module rather than inspecting EF's change tracker.

## Surface seams

CLI tests inject `TextReader`/`TextWriter` and invoke the command graph in
process. TUI tests operate on workspace state and bounded rendering, with the
Terminal.Gui host covered separately. Desktop tests inject `IUiDispatcher`,
dirty-navigation confirmation, and controlled completions; headless tests also
instantiate the real Avalonia controls. Cross-surface durability tests always
dispose one host before opening the next against the same file.

Run a focused project while developing and the full solution before handoff:

```sh
dotnet test tests/Mixology.Modules.Orders.Tests
dotnet test Mixology.slnx
```
