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
npm ci --ignore-scripts
npm run lint:spelling
dotnet tool restore
dotnet restore Mixology.slnx
MIXOLOGY_TEST_ORDER_SEED=local dotnet build Mixology.slnx --no-restore
MIXOLOGY_TEST_ORDER_SEED=local dotnet test Mixology.slnx --no-build
dotnet format Mixology.slnx --verify-no-changes --no-restore
dotnet run --project tools/Mixology.DispatchGenerator --no-build -- \
  --manifest src/Mixology.Dispatcher/dispatcher.routes.json \
  --output src/Mixology.Dispatcher/Generated/DomainEventDispatcher.g.cs --check
dotnet ef migrations has-pending-model-changes --project src/Mixology.Migrations --no-build
```

GitHub Actions repeats this gate for every pull request and every push to
`master`, then publishes and executes the native Desktop help path on Linux x64,
Windows x64, and macOS x64 runners. Its Linux job also runs the SharpDetect
dynamic race gate over the desktop concurrency primitives. The workflow pins
official actions by immutable release commit. See
[Development](docs/development.md) for the local race command and test-order
seed reproduction.

The repository pins the .NET 10 SDK and treats compiler, analyzer, and configured
style warnings as errors. C# 14 is deliberate: native C# discriminated unions are
not available in the pinned stable toolchain, so closed unions use explicit
record hierarchies and exhaustive pattern matching.

## Teaching guides

- [Architecture](docs/architecture.md) explains module direction, operations,
  authorization, transactions, and generated event reactions.
- [Application features](docs/features.md) traces filtering, tags, personas,
  audit, fulfillment, retirement, and runtime configuration.
- [Development](docs/development.md) covers setup, validation, generation,
  migrations, and production-shaped tests.
- [Source map](src/README.md) introduces the kernel, bounded contexts, and
  independent presentation surfaces.
- [Documentation parity map](docs/documentation-map.md) maps every teaching
  README in the Go reference to its semantic .NET destination, including the
  intentionally consolidated toolkit topics.

## Repository shape

```text
src/
  Mixology.Kernel/                 shared domain value types
  Mixology.Application/            host composition and sessions
  Mixology.Filtering/              Expr adapter and LINQ translation
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

## Terminal application

The standalone TUI provides the live Dashboard plus complete Drinks,
Ingredients, Inventory, Menus, Orders, Audit, and Tags workspaces:

```sh
dotnet run --project src/Mixology.Tui -- --db data/mixology.db --actor owner
```

It uses an instance-owned Terminal.Gui application, keeps diagnostics in
`mixology-tui.log` beside the database by default, and supports the same
`MIXOLOGY_DB`, `MIXOLOGY_ACTOR`, logging, and metrics configuration as the
reference process. Cedar filters navigation and workspace actions for the
selected actor; the shell advertises only routes that are both authorized and
backed by a registered workspace factory. Every workspace has deterministic
browse/detail rendering, forms, contextual filter help, cursor paging, stable
selection, stale-response rejection, and cancellation-aware shutdown. The CLI
and TUI share the same SQLite file, so writes are visible after either process
reopens the store.

## Desktop client

The Avalonia desktop composition root exposes Dashboard, Drinks, Ingredients,
Inventory, Menus, Orders, Audit, and Tags through authorization-visible
navigation:

```sh
dotnet run --project src/Mixology.Desktop -- --db data/mixology.db --actor owner
```

It shares no view models with the TUI. Avalonia-native MVVM state owns UI-thread
publication, latest-request-wins refresh, an owned dirty-navigation dialog, and
drained shutdown; headless tests exercise the real controls. Desktop accepts
the CLI-equivalent `--log-level`, `--log-format`, `--log-file`, and `--metrics`
options plus their `MIXOLOGY_*` environment defaults. The application host owns
diagnostic file handles and the optional `localhost:9090/metrics` listener for
the full window lifetime. Production-shaped tests mutate through CLI, TUI, and
Desktop in both directions against one durable SQLite store.
