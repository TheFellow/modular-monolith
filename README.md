# Mixology Modular Monolith for .NET

This repository is a semantic C# port of
[`go-modular-monolith`](https://github.com/TheFellow/go-modular-monolith). It is
being rebuilt as a teaching application on .NET 10, not transliterated from Go.
The reference behavior remains one stateful cocktail-bar application with seven
bounded contexts, one embedded database, Cedar authorization, and independent
CLI, TUI, and desktop adapters.

The abandoned prototype remains available through Git history. The new codebase
starts from explicit decisions in [`.ai/prior-art`](.ai/prior-art/) and grows in
tested vertical slices. See [the port roadmap](docs/roadmap.md) and
[semantic parity ledger](docs/semantic-parity.md).

## Development loop

```sh
dotnet tool restore
dotnet restore Mixology.slnx
dotnet build Mixology.slnx --no-restore
dotnet test Mixology.slnx --no-build
dotnet format Mixology.slnx --verify-no-changes --no-restore
dotnet run --project tools/Mixology.DispatchGenerator --no-build -- \
  --manifest src/Mixology.Dispatcher/dispatcher.routes.json \
  --output src/Mixology.Dispatcher/Generated/DomainEventDispatcher.g.cs --check
dotnet ef migrations has-pending-model-changes --project src/Mixology.Migrations --no-build
```

The repository pins the .NET 10 SDK and treats compiler, analyzer, and configured
style warnings as errors. C# 14 is deliberate: native C# discriminated unions are
not available in the pinned stable toolchain, so closed unions use explicit
record hierarchies and exhaustive pattern matching.

## Repository shape

```text
src/
  Mixology.Kernel/                 shared domain value types
  Mixology.Application/            host composition and sessions
  Mixology.Filtering/              checked filter AST and LINQ translation
  Mixology.Persistence/            EF Core and SQLite unit of work
  Mixology.Migrations/             design-time model and checked-in migrations
  Mixology.Authorization.Cedar/    cedar-dotnet adapter
  Mixology.Modules.*/              seven bounded contexts
  Mixology.Dispatcher/             generated event routing
  Mixology.Toolkits.*/             presentation-only mechanics
  Mixology.Cli|Tui|Desktop|Seed/   process composition roots
tests/
  ...                              unit, architecture, integration, and surface tests
tools/
  Mixology.DispatchGenerator/      deterministic committed code generation
```

Public module roots are facades. Models, queries, and events are deliberate
cross-domain contracts. Commands, persistence rows, and handlers stay internal.
Presentation projects consume public application behavior and never another
surface's implementation.

## Canonical sample data

The standalone seed process creates the reference set of 18 ingredients and
inventory records, six classic drinks, and one published menu through the same
authorized, audited module APIs used by the other surfaces:

```sh
dotnet run --project src/Mixology.Seed
```

It writes to `data/mixology.db` by default. Set `MIXOLOGY_DB` to select another
path. Like the Go reference, the seed is deliberately non-idempotent and
command-atomic: running it against an already seeded store exits with an error,
while work committed before any later failure remains in the database.

## Terminal dashboard

The standalone TUI currently provides the production composition root,
authorization-filtered route shell, and live Dashboard foundation:

```sh
dotnet run --project src/Mixology.Tui -- --db data/mixology.db --actor owner
```

It uses an instance-owned Terminal.Gui application, keeps diagnostics in
`mixology-tui.log` beside the database by default, and supports the same
`MIXOLOGY_DB`, `MIXOLOGY_ACTOR`, logging, and metrics configuration as the
reference process. Domain workspaces are being added as independently tested
vertical slices; unimplemented routes are not advertised by the shell.
