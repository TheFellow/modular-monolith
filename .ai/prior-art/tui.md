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

## Implementation consequences

The completed terminal application keeps each bounded context in its own
workspace and uses the toolkit-neutral presentation projectors for action
visibility and enablement. Drinks owns its recipe editor; Ingredients owns
retirement replacement intent; Inventory owns amount, cost, and adjustment
reason forms; Menus and Orders expose their coupled lifecycle operations; Audit
is append-only and owner-only; Tags provides owner-only discovery. Forms capture
text before global shortcuts, local Escape closes the active mode before shell
navigation, and duplicate submission is gated by the form state machine.

Lists retain stable typed-ID selection across refreshes, use the same checked
filter language and cursor contracts as the CLI, and render within a fixed
80-by-21 workspace viewport inside the shell's 80-by-24 minimum terminal.
Requests carry generations and linked cancellation sources; stale completions
cannot publish, and disposal cancels then observes all tracked work. Unknown
exceptions are normalized to a safe `InternalError`, existing `AppError`
instances keep their identity and user message, and cancellation remains
distinct.

Production-composition tests project navigation through real Cedar policies for
owner, manager, and anonymous actors, then mount every advertised route. This
guards both halves of the route invariant: unauthorized Audit and Tags routes
stay absent, while no visible route can point at a missing or mismatched factory.
Cross-surface tests use a real SQLite file in both directions: a mutation through
the TUI workspace is read by a separately launched CLI process, and a CLI-process
mutation is read by a newly opened TUI host and workspace. Process completion
and workspace task-drain seams make these tests deterministic without sleeps.

## Sources

- [Terminal.Gui repository and MIT license](https://github.com/gui-cs/Terminal.Gui)
- [Terminal.Gui 2.4.17 package metadata](https://www.nuget.org/packages/Terminal.Gui/2.4.17)
- [Instance application architecture and lifecycle](https://tui-cs.github.io/Terminal.Gui/docs/application)
- [Terminal.Gui layout](https://tui-cs.github.io/Terminal.Gui/docs/layout)
- [Keyboard testing](https://tui-cs.github.io/Terminal.Gui/docs/keyboard.html)
- [Spectre prompt limitations](https://spectreconsole.net/console/how-to/prompting-for-user-input/)
- [Spectre live-display limitations](https://spectreconsole.net/console/live/live-display/)
