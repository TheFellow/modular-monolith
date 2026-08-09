# Terminal user interface

Status: Accepted
Date: 2026-08-09

## Decision

Use Terminal.Gui v2, pinned to stable version 2.4.17 as of 2026-08-09,
behind a small Mixology toolkit. Spectre.Console remains a CLI renderer because
its prompts and live displays are not composable into the persistent nested
forms, dialogs, focus ownership, and navigation required here.

The port will be semantic, not a Bubble Tea API imitation. Each process creates
and disposes its own `IApplication` and driver through `Application.Create()`;
the obsolete static singleton is forbidden. Terminal.Gui's views, commands,
and events own native TUI state. Domain
view models retain serialized publication, request generations, stale-response
rejection, local-before-global Escape handling, contextual help, and pure
rendering. They do not share GUI view models.

Tests use a virtual time provider, ANSI driver, injected keys, fixed terminal
sizes, and captured buffers. The minimum parity set covers viewport bounds,
resize, nested input ownership, browse/edit modes, paging, tags, authorization,
stale work, and deterministic shutdown.

The production executable is a leaf Generic Host composition root. Its route
model retains the reference identities and stable `1` through `7` workspace
shortcuts, while Dashboard remains the initial/back destination without an
invented shortcut. Authorization projection is intersected with registered
workspace factories so an incremental build never advertises a dead route.
Each Dashboard refresh owns a generation and cancellation source; superseded
work is retained and observed during shutdown even when its response is stale.

## Sources

- [Terminal.Gui repository and MIT license](https://github.com/gui-cs/Terminal.Gui)
- [Terminal.Gui 2.4.17 package metadata](https://www.nuget.org/packages/Terminal.Gui/2.4.17)
- [Instance application architecture and lifecycle](https://tui-cs.github.io/Terminal.Gui/docs/application)
- [Terminal.Gui layout](https://tui-cs.github.io/Terminal.Gui/docs/layout)
- [Keyboard testing](https://tui-cs.github.io/Terminal.Gui/docs/keyboard.html)
- [Spectre prompt limitations](https://spectreconsole.net/console/how-to/prompting-for-user-input/)
- [Spectre live-display limitations](https://spectreconsole.net/console/live/live-display/)
