# Terminal application

`Mixology.Tui` is the Terminal.Gui composition root and owns the live workspace
shell. It consumes public modules, shared presentation projections, and
[`Mixology.Toolkits.Tui`](../Mixology.Toolkits.Tui/README.md), but shares no
views or view models with Desktop.

## Run

```sh
dotnet run --project . -- --db ../../data/mixology.db --actor owner
```

The process accepts database, actor, logging, and metrics options equivalent to
the CLI. Diagnostics default to `mixology-tui.log` beside the database so they
do not corrupt terminal rendering. The host owns the optional Prometheus
listener for the entire terminal lifetime.

## Architecture

`TuiHost` composes the Generic Host and modules. `NavigationProjector` filters
routes through Cedar, and `TuiApplication` registers only workspaces with real
factories. `TuiShell` lazily creates and caches those workspaces, owns navigation
history/help/status, and drains workspace disposal before the application host
closes.

Every workspace implements `ITuiWorkspace` and owns its list/detail state,
typed filter, paging cursor, forms, and asynchronous refresh generation. It
renders deterministic text into a bounded `Viewport`; `TerminalGuiRunner`
adapts that state to the actual Terminal.Gui application instance. There is no
static global terminal lifecycle.

## Interaction model

- `1` through `7` open Drinks, Ingredients, Inventory, Menus, Orders, Audit,
  and Tags when authorized.
- `r` refreshes, `?` toggles route/help hints, `Esc` backs out, and `q` quits.
- Browse workspaces commonly use `j`/`k` or arrows, Enter for selection, and
  surface-advertised action keys.
- Forms own text input; `Tab` changes field, `Ctrl+S` submits, and `Esc` cancels.

Local input is routed before global input. An editing form therefore consumes
letters such as `q` instead of quitting, and consumes `Esc` to cancel rather
than navigating. The status line presents `AppError.UserMessage` with the
catalog's information/warning/error style.

## Asynchrony and lifecycle

Refreshes use latest-request-wins generations: a slower obsolete response may
finish but cannot replace newer state. Cancellation is linked to workspace and
shell lifetime. Shutdown stops new work, cancels requests, observes/drains them,
disposes cached workspaces, then disposes Terminal.Gui and the Generic Host.

The shell requires at least 80×24 cells and renders a clear minimum-size message
below that. Tests exercise deterministic workspace rendering, input ownership,
authorization-filtered routes, stale work, and cancellation without depending
on a developer's real terminal.

## Adding a workspace

Create an `ITuiWorkspace`, add its route/factory only after the matching
capability exists, and keep its module calls behind public APIs. Cover browse,
empty/error states, filtering, paging, forms/dialogs, action projection,
stale-response rejection, and disposal. Registration alone must never make an
unauthorized route visible.
