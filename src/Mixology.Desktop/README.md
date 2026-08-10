# .NET MAUI desktop application

`Mixology.Desktop` is a .NET MAUI MVVM composition root for Windows and macOS.
It provides Dashboard, Drinks, Ingredients, Inventory, Menus, Orders, Audit, and
Tags through the same application modules and Cedar capabilities as CLI and TUI.
Linux continues to build and test the UI-neutral `net10.0` target, but no Linux
GUI is produced.

## Boundaries and lifecycle

`DesktopHost` owns Generic Host, persistence, modules, Serilog, OpenTelemetry,
and the generated dispatcher. `DesktopShellFactory` projects actor-visible
navigation and registers only implemented workspace factories. `ShellViewModel`
lazily creates and caches workspaces, activating Dashboard first.

The MAUI platform host owns `DesktopSession`. Closing the native window disposes
workspaces in reverse order, stops Generic Host, flushes diagnostics, and releases
the SQLite file. `MauiUiDispatcher` marshals observable publication to the native
UI thread, and `MauiDirtyNavigationConfirmation` uses the platform alert API.
The `.xaml` views use compiled bindings and contain only MAUI layout concerns.

## Prerequisites and running

Install the .NET 10 MAUI workload on a supported host:

```sh
dotnet workload restore Mixology.Desktop.csproj -p:BuildNativeDesktop=true
```

Run on macOS:

```sh
dotnet run -p:BuildNativeDesktop=true -f net10.0-maccatalyst -- --db ../../data/mixology.db --actor owner
```

Run on Windows:

```powershell
dotnet run -p:BuildNativeDesktop=true -f net10.0-windows10.0.19041.0 -- --db ../../data/mixology.db --actor owner
```

Options are `--db`, `--actor`/`--as`, `--log-level`, `--log-format`,
`--log-file`, and `--metrics`, with `MIXOLOGY_*` environment defaults. When
enabled, Prometheus is available at `localhost:9090/metrics` for the window
lifetime.

## Build, publish, and test

```sh
# UI-neutral build and tests; works on Linux, macOS, and Windows
dotnet build Mixology.Desktop.csproj -f net10.0
dotnet test ../../tests/Mixology.Desktop.Tests

# Native macOS publish (use the Windows framework and win-x64 on Windows)
dotnet publish Mixology.Desktop.csproj -c Release \
  -p:BuildNativeDesktop=true -f net10.0-maccatalyst \
  -r maccatalyst-x64 --self-contained
```

View-model tests inject an immediate or recording dispatcher, dirty-navigation
confirmation, and controlled async calls. Markup tests verify all workspace
views remain MAUI XAML with compiled binding declarations. Native CI publishes
both supported desktop targets; cross-surface tests exercise the production host
against the same durable SQLite database as CLI and TUI.
