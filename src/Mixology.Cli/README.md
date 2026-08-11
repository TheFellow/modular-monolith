# CLI entrypoint and toolkit

`Mixology.Cli` is both the `System.CommandLine` composition root and the home of
the small amount of CLI-specific rendering needed by this application. The Go
repository separated a generic CLI/table toolkit; the .NET port deliberately
uses `System.CommandLine`, `TextWriter`, and domain-local render functions
instead of wrapping those mature APIs in another project.

## Request path

```text
System.CommandLine parser -> hosted command session -> public module request
                          -> application pipeline -> typed result/AppError
                          -> text or JSON + stable exit code
```

Each command opens the Generic Host with the selected database, actor, logging,
and metrics configuration, initializes migrations, creates an actor-bound
`MixologySession`, and disposes all resources when the invocation ends. Command
tests inject input/output/error streams and production-shaped session factories;
business behavior remains in modules.

## Run and discover

```sh
dotnet run --project . -- --help
dotnet run --project . -- --db ../../data/mixology.db status
dotnet run --project . -- drinks list --filter-help
dotnet run --project . -- --actor bartender menus list
```

The root exposes `status`, `drinks`, `ingredients`, `inventory`, `menus`,
`orders`, `audit`, and `tags`. Global options include `--db`, `--actor`/`--as`,
`--log-level`, `--log-format`, `--log-file`, and `--metrics`. Commands expose
their own paging, filtering, document input, confirmation, and JSON options.
Prefer command `--help` over copying a static option list into documentation.

Human-readable lists use stable, ordinal columns assembled by the matching
command file. JSON serialization uses camel-case names, indentation, and omission
of null fields. Machine output goes to stdout; diagnostics and safe errors go to
stderr. `CliErrorAdapter` maps typed errors to their catalog exit code and
`UserMessage`; unexpected errors use the generic process failure code.

Replace-style JSON updates for drinks, ingredients, and menus must round-trip
the positive `revision` returned by a read. Treat it as an opaque concurrency
token rather than incrementing it in a script. Persistence returns the standard
typed conflict when another client has committed a newer revision. Flag-based
ingredient, inventory, and menu mutations load the current revision immediately
before submission; explicit document input retains read/edit/write conflict
detection for automation.

## Adding a command

1. Add or reuse a public request/query in the owning module.
2. Define arguments/options and the handler in that module's `*Commands.cs`.
3. Open the matching hosted session and execute through `MixologySession`.
4. Keep text rendering deterministic and add JSON only from public models.
5. Catch at the command boundary with `CliErrorAdapter` and never print raw
   internal exception messages.
6. Add parser, success, typed-failure, authorization, and restart tests in
   `Mixology.Cli.Tests`.

Do not let CLI option types or writers enter domain projects. If another
surface needs the same aggregate or action, move that behavior into a module or
`Mixology.Presentation`, not into a shared CLI abstraction.
