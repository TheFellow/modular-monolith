# Authorization

Status: Accepted  
Date: 2026-08-09

## Decision

Use the sibling [`cedar-dotnet`](https://github.com/TheFellow/cedar-dotnet)
implementation rather than cedar-go, a subprocess, or an RPC boundary. Domain
assemblies own `.cedarschema` and `.cedar` resources plus explicit adapters from
domain models to Cedar entities. A small `Mixology.Authorization.Cedar` assembly
assembles and validates policies and translates Cedar decisions into typed
application errors.

The initial integration uses source project references so both repositories can
evolve together and the complete implementation compiles as .NET. Before the
first authorization commit, `cedar-dotnet` will be pinned reproducibly, either
as released packages or a repository submodule; an unversioned sibling path is
not an acceptable final build input.

Commands authorize both loaded and resulting entities in the unit of work.
Gets authorize their result; lists omit only denied rows and continue filling
the page. Policy also shapes route discovery, aggregates, and concrete action
availability, while commands remain authoritative against stale clients.

## Validation

- Validate every assembled policy against every domain schema at startup/tests.
- Port the persona and ABAC matrix from the Go repository.
- Prove denial hides rows, totals, routes, and actions without masking evaluator
  or infrastructure failures.
- Prove a bypassed or stale presentation action is denied by the command.

## Sources

- [cedar-dotnet](https://github.com/TheFellow/cedar-dotnet)
- [Cedar policy language](https://docs.cedarpolicy.com/)
- [Cedar authorization model](https://docs.cedarpolicy.com/auth/authorization.html)

