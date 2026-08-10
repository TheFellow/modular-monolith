# Application errors

The error family is the semantic port of Go's cross-cutting `pkg/errors` design.
Domain, persistence, middleware, and presentation code throw one typed
`AppError`; process edges translate its immutable kind into stable behavior.
Diagnostic detail stays available through `Message`, `InnerException`, and
normal .NET exception traversal, while user output uses `UserMessage`.

## Kinds and mappings

| Kind | Default message | HTTP | gRPC | CLI | terminal style |
| --- | --- | ---: | ---: | ---: | --- |
| `Invalid` | `invalid` | 400 | 3 | 10 | error |
| `NotFound` | `not found` | 404 | 5 | 20 | warning |
| `Permission` | `permission denied` | 403 | 7 | 30 | error |
| `Conflict` | `conflict` | 409 | 6 | 40 | warning |
| `FailedPrecondition` | `failed precondition` | 412 | 9 | 45 | warning |
| `Internal` | `internal error` | 500 | 13 | 50 | error |

`ErrorCatalog` is the single mapping table. The numeric transport values are
metadata; the kernel does not start a server, write to a terminal, or depend on
an RPC package.

## Constructing and inspecting

Choose a kind by application meaning, not by the current surface:

```csharp
if (string.IsNullOrWhiteSpace(name))
{
    throw AppError.Invalid("name is required");
}

Drink? drink = await FindAsync(id, cancellationToken);
if (drink is null)
{
    throw AppError.NotFound($"drink {id} not found");
}
```

The factory returns a concrete type such as `InvalidError` or `NotFoundError`.
Use pattern matching when the concrete variant matters, or the traversal helpers
when errors may be wrapped or aggregated:

```csharp
if (AppError.IsNotFound(exception)) { /* recover or translate */ }
AppError? applicationError = AppError.Find(exception);
```

Always retain a dependency failure as the `cause`/`InnerException`. Do not
parse messages or map an arbitrary exception to `Invalid` merely because it
occurred while handling input.

## Safe presentation

For non-internal kinds, `UserMessage` defaults to the actionable diagnostic
message. An internal error defaults to the generic `internal error`, preventing
database details, paths, or credentials from reaching the user. Use
`WithUserMessage` to supply an explicit safe recovery message:

```csharp
throw AppError.Internal("load inventory", exception)
    .WithUserMessage("Inventory is temporarily unavailable; please try again.");
```

CLI and TUI adapters may use `CliExitCode` and `TerminalStyle`; the Desktop maps
the same kind into inline/warning/error presentation. Lower layers must not pick
exit codes, colors, or dialogs themselves. Unexpected exceptions remain exit
code 1 at the outermost CLI boundary and should be logged diagnostically.

## Adding a kind

Add the `ErrorKind`, catalog entry, concrete record in `TypedErrors.cs`, factory
and classifier in `AppError`, then update kernel and all surface-adapter tests.
Keep the set closed: introducing a kind is a repository-wide semantic change,
not a local convenience.
