# TUI toolkit

`Mixology.Toolkits.Tui` contains the reusable terminal mechanics needed by
Mixology without referencing a bounded context or executable. The Go reference
split components, dialogs, forms, key names, key maps, and styles into folders;
the idiomatic .NET port keeps the small cohesive types in one project and
documents those concepts here.

## Host and component lifecycle

`TerminalApplicationHost` owns one Terminal.Gui `IApplication` and enforces
`Created -> Initialized -> Running -> Initialized -> Disposed`. It can select
the ANSI driver for deterministic environments and refuses disposal while a run
is active. `TuiCommandQueue` serializes UI commands, permits a running command
to enqueue follow-up work, rejects re-entrant drains, and caps a drain to catch
accidental infinite cycles.

`Viewport`, `Insets`, and `TuiLayout` calculate bounded content and stable
list/detail splits without touching global terminal state. Applications compose
these mechanics into their own components rather than inheriting a toolkit
base view.

## Tables and selection

`TableModel<T,TKey>` defines typed columns and an `ITableSource`, requires unique
headers/row keys, and preserves selection by stable key after refresh. If the
selected row disappears it selects the nearest valid index; an empty table has
no selection. This is the semantic replacement for both the Go CLI table helper
and TUI table component—the CLI uses direct text tables, while Terminal.Gui
needs a stateful selection model.

## Forms and dialogs

`FormModel` has explicit `Browse`, `Edit`, and `Submitting` modes. It tracks a
baseline, dirty values, field validation, submission failures, and cancellation
rollback. Mutation outside the correct state is a lifecycle error, preventing a
late completion from silently changing an abandoned edit.

Confirmation dialogs are surface-owned modes built from the same state rules:
Enter or `Ctrl+S` confirms; `Esc` cancels. The toolkit intentionally does not
know domain verbs or create modal UI itself.

## Keys, key names, and routing

Terminal.Gui's `Key` is the canonical key representation; no parallel string
key enum is maintained. `InputRouter.Dispatch` gives the local owner first
chance, then global navigation. `InputOwnership.Edit` captures text and Escape,
while `Browse` permits route shortcuts. Help text should display conventional
names (`Esc`, `Enter`, `Tab`, `Ctrl+S`) beside the action at the point of use.

## Styles and errors

Semantic error styling comes from `AppError.TerminalStyle`, not from arbitrary
domain colors. The executable owns the actual Terminal.Gui theme and status
rendering. Layout and state tests should assert semantic information and
deterministic text rather than terminal escape sequences or a particular color
palette.

## Fast path

Use `FormModel` for editable values, `TableModel` when stable selection matters,
`InputRouter` at the local/global boundary, and `TerminalApplicationHost` only
at the composition root. Add a toolkit abstraction only when at least two
workspaces need the same interaction invariant; domain-specific actions stay in
the workspace.
