# Development

## Prerequisites

- The .NET SDK selected by [`global.json`](../global.json).
- The local tools declared in [`.config/dotnet-tools.json`](../.config/dotnet-tools.json).
- Node.js 22.18 or newer and npm for the pinned spelling check.
- Native desktop prerequisites are not needed for ordinary builds; Avalonia's
  managed headless driver covers control tests.

Restore the pinned tools and dependencies with:

```sh
dotnet tool restore
dotnet restore Mixology.slnx
npm ci --ignore-scripts
```

## Everyday loop

```sh
dotnet build Mixology.slnx --no-restore
dotnet test Mixology.slnx --no-build
dotnet format Mixology.slnx --verify-no-changes --no-restore
npm run lint:spelling
```

The repository enables nullable analysis, SDK and third-party analyzers,
configured style rules, deterministic builds, and warnings as errors. Prefer
the formatter and analyzer fixes over local suppression. When a suppression is
necessary, keep it as narrow as possible and explain the invariant it protects.

## Full validation

The CI-equivalent commands are documented in [the root README](../README.md#development-loop).
In addition to build, tests, and formatting, the gate checks that:

- dispatcher output agrees with `dispatcher.routes.json`;
- the EF Core model has no migration missing from source control;
- architecture tests still enforce project and namespace direction;
- spelling is clean and tests run in a seeded, reproducible randomized order;
- SharpDetect reports no managed field races in the focused concurrency tests;
- each supported native desktop target can be published and execute `--help`.

Generated files are source artifacts, not build debris. Change the route
manifest, run the generator without `--check`, inspect the result, and commit
both files together:

```sh
dotnet run --project tools/Mixology.DispatchGenerator -- \
  --manifest src/Mixology.Dispatcher/dispatcher.routes.json \
  --output src/Mixology.Dispatcher/Generated/DomainEventDispatcher.g.cs
```

Create EF changes through the migrations project:

```sh
dotnet ef migrations add DescribeTheChange \
  --project src/Mixology.Migrations \
  --startup-project src/Mixology.Migrations
```

## Running the application

Seed a disposable database, then point any surface at it:

```sh
MIXOLOGY_DB=data/mixology.db dotnet run --project src/Mixology.Seed
dotnet run --project src/Mixology.Cli -- --db data/mixology.db status
dotnet run --project src/Mixology.Tui -- --db data/mixology.db --actor owner
dotnet run --project src/Mixology.Desktop -- --db data/mixology.db --actor owner
```

The database uses SQLite WAL mode. Tests and applications must dispose their
host/session before deleting the file or starting an operation that assumes
exclusive ownership.

## Testing shape

Tests intentionally use the production composition root. Persistence tests get
a path below a fresh temporary directory, initialize real migrations, and
dispose the host after each test. Cross-surface tests open CLI, TUI, and Desktop
against the same file sequentially to prove durable behavior without sharing UI
state.

Use focused project tests while iterating, then run the complete solution:

```sh
dotnet test tests/Mixology.Filtering.Tests
dotnet test tests/Mixology.Application.Tests
dotnet test Mixology.slnx
```

CI sets and prints `MIXOLOGY_TEST_ORDER_SEED`. Set it locally on both build and
test to reproduce the same v2 and v3 xUnit ordering:

```sh
MIXOLOGY_TEST_ORDER_SEED=123456-1 dotnet build Mixology.slnx
MIXOLOGY_TEST_ORDER_SEED=123456-1 dotnet test Mixology.slnx --no-build
```

The dynamic race gate is available on Windows and Linux x64 after `dotnet tool
restore`:

```sh
dotnet sharpdetect run eng/sharpdetect-latest-request.json
```

The checked-in configuration instruments the production desktop toolkit while
excluding the VSTest host, test assembly, and third-party test infrastructure.
This keeps the gate focused on races in `LatestRequest<T>` instead of reporting
unsupported publication patterns inside the runner.

See the [linting prior-art record](../.ai/prior-art/linting.md) for the exact
coverage and residual differences from Go's race detector.

Domain tests belong beside their matching module test project. Infrastructure
contracts have dedicated test projects, and dependency-direction rules belong
in `Mixology.Architecture.Tests`. See [the test guide](../tests/README.md) for
fixtures and surface-specific seams.
