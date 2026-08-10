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

The integration uses source project references to the `cedar-dotnet` submodule,
pinned at commit `1d0cf8efe0e6b829b89742b1538e0b86244363db`. Reference
`Cedar.Ast` for parsing/evaluation and `Cedar.Schema` for validation; referencing
only `Cedar.Core` omits evaluator code in the current project layout.

Commands authorize both loaded and resulting entities in the unit of work.
Gets authorize their result; lists omit only denied rows and continue filling
the page. Policy also shapes route discovery, aggregates, and concrete action
availability, while commands remain authoritative against stale clients.

## Validation

- Validate base policies against the base schema and each domain policy against
  its owning schema; cross-validating a domain policy against unrelated schemas
  incorrectly rejects valid resource/action types.
- Port the persona and ABAC matrix from the Go repository.
- Prove denial hides rows, totals, routes, and actions without masking evaluator
  or infrastructure failures.
- Prove a bypassed or stale presentation action is denied by the command.

## Sources

- [cedar-dotnet](https://github.com/TheFellow/cedar-dotnet)
- [Cedar policy language](https://docs.cedarpolicy.com/)
- [Cedar authorization model](https://docs.cedarpolicy.com/auth/authorization.html)
