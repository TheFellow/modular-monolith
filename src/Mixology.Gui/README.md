# .NET MAUI desktop application

`Mixology.Gui` is a .NET MAUI MVVM composition root for Windows and macOS.
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

`ShellViewModel` consumes the persistence store's coalesced SQLite change
epochs. Clean active workspaces re-query immediately using their existing
latest-request-wins loader, and inactive cached workspaces reload on their next
activation. A dirty editor is marked stale but preserved; clearing or cancelling
the editor triggers the deferred refresh. If it is saved first, its revision is
compared by EF and a concurrent update is presented as a typed conflict instead
of silently overwriting the other client.

## Prerequisites and running

Install the .NET 10 MAUI workload on a supported host:

```sh
dotnet workload restore Mixology.Gui.csproj -p:BuildNativeGui=true
```

Run on macOS:

```sh
dotnet run -p:BuildNativeGui=true -f net10.0-maccatalyst -- --db ../../data/mixology.db --actor owner
```

Run on Windows:

```powershell
dotnet run -p:BuildNativeGui=true -f net10.0-windows10.0.19041.0 -- --db ../../data/mixology.db --actor owner
```

Options are `--db`, `--actor`/`--as`, `--log-level`, `--log-format`,
`--log-file`, and `--metrics`, with `MIXOLOGY_*` environment defaults. When
`--db` and `MIXOLOGY_DB` are omitted, the GUI stores its database under the
current user's local application-data directory rather than inside the signed
application bundle. When enabled, Prometheus is available at
`localhost:9090/metrics` for the window lifetime.

## Build, publish, and test

```sh
# UI-neutral build and tests; works on Linux, macOS, and Windows
dotnet build Mixology.Gui.csproj -f net10.0
dotnet test ../../tests/Mixology.Gui.Tests

# Native macOS publish (use the Windows framework and win-x64 on Windows)
dotnet publish Mixology.Gui.csproj -c Release \
  -p:BuildNativeGui=true -f net10.0-maccatalyst \
  -r maccatalyst-x64 --self-contained
```

View-model tests inject an immediate or recording dispatcher, dirty-navigation
confirmation, and controlled async calls. Markup tests verify all workspace
views remain MAUI XAML with compiled binding declarations. Native CI publishes
both supported desktop targets; cross-surface tests exercise the production host
against the same durable SQLite database as CLI and TUI.
