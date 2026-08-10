# Avalonia desktop application

`Mixology.Desktop` is an Avalonia MVVM composition root. It provides Dashboard,
Drinks, Ingredients, Inventory, Menus, Orders, Audit, and Tags workspaces through
the same application modules and Cedar capabilities as CLI and TUI, without
sharing either surface's view models.

## Boundaries and information architecture

`DesktopHost` owns Generic Host, persistence, modules, Serilog, OpenTelemetry,
and the generated dispatcher. `DesktopShellFactory` projects actor-visible
navigation and registers only implemented workspace factories. `ShellViewModel`
then lazily creates/caches workspaces and activates Dashboard first.

Each workspace owns its public module calls, paging/filter state, form state,
commands, and status. `.axaml` views use compiled bindings and contain only
Avalonia layout/control concerns. Cross-surface aggregates, navigation, and
actions come from `Mixology.Presentation`; UI-thread and latest-request mechanics
come from [`Mixology.Toolkits.Desktop`](../Mixology.Toolkits.Desktop/README.md).

Dirty navigation is explicit. An `IDirtyNavigationConfirmation` owned by the
Avalonia application displays the modal; cancel keeps the current workspace and
confirm discards its draft. A view model never constructs a window directly.

## Run and configure

```sh
dotnet run --project . -- --db ../../data/mixology.db --actor owner
dotnet run --project . -- --help
```

Options are `--db`, `--actor`/`--as`, `--log-level`, `--log-format`,
`--log-file`, and `--metrics`, with `MIXOLOGY_*` environment defaults. Explicit
arguments win. When enabled, Prometheus is available at
`localhost:9090/metrics` for the window lifetime.

## Lifecycle and asynchronous work

All observable publication is marshalled through `IUiDispatcher`.
`LatestRequest<T>` cancels the prior generation and marks late completions as
superseded; view models publish only current results. Closing the last window
cancels and drains workspace work, disposes workspaces in reverse order, stops
the Generic Host, flushes diagnostics, and releases the SQLite file.

## Build, publish, and test

```sh
dotnet build Mixology.Desktop.csproj
dotnet publish Mixology.Desktop.csproj -c Release -r linux-x64 --self-contained
```

Replace the RID with `win-x64` or `osx-x64` for the other CI targets. CI
executes the published `--help` path on a native runner, proving startup without
opening a window.

Headless Avalonia tests instantiate real controls and compiled bindings.
View-model tests inject an immediate/recording dispatcher, dirty-navigation
confirmation, and controlled async calls. Cross-surface tests write through one
surface, dispose it, then reopen the same SQLite file through another.
