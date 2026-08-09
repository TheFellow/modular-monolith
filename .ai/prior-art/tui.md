# Terminal user interface

Status: Accepted  
Date: 2026-08-09

## Decision

Use Terminal.Gui v2, pinned to a stable version when the TUI slice begins,
behind a small Mixology toolkit. Spectre.Console remains a CLI renderer because
its prompts and live displays are not composable into the persistent nested
forms, dialogs, focus ownership, and navigation required here.

The port will be semantic, not a Bubble Tea API imitation. Terminal.Gui's
instance application, views, commands, and events own native TUI state. Domain
view models retain serialized publication, request generations, stale-response
rejection, local-before-global Escape handling, contextual help, and pure
rendering. They do not share GUI view models.

Tests use a virtual time provider, ANSI driver, injected keys, fixed terminal
sizes, and captured buffers. The minimum parity set covers viewport bounds,
resize, nested input ownership, browse/edit modes, paging, tags, authorization,
stale work, and deterministic shutdown.

## Sources

- [Terminal.Gui repository and MIT license](https://github.com/tui-cs/Terminal.Gui)
- [Terminal.Gui drivers](https://tui-cs.github.io/Terminal.Gui/api/Terminal.Gui.Drivers.html)
- [Keyboard testing](https://tui-cs.github.io/Terminal.Gui/docs/keyboard.html)
- [Spectre prompt limitations](https://spectreconsole.net/console/how-to/prompting-for-user-input/)
- [Spectre live-display limitations](https://spectreconsole.net/console/live/live-display/)

