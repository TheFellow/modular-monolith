# Sample-data seed process

Status: Accepted
Date: 2026-08-09

## Decision

Keep seed data in a dedicated `Mixology.Seed` executable. Compose the same .NET
Generic Host and public module facades as the interactive surfaces, and embed the
canonical JSON in the assembly. Parse it with source-generated
`System.Text.Json` metadata. No fixture, fake repository, direct EF write, or
third-party data-generation package participates in the production seed path.

The executable preserves the reference's deliberately non-idempotent behavior:
commands commit independently, a restart fails on the first duplicate, and
earlier successful work remains committed. It uses the reference default
`data/mixology.db`, honors `MIXOLOGY_DB`, writes `error: ...` to stderr, and
returns exit code 1 for every failure. It does not add a parallel command-line
parser or adopt the main CLI's typed exit-code convention.

Embedded resources make the executable self-contained without copying mutable
content beside the binary. Source-generated JSON metadata keeps that boundary
compatible with trimming and future self-contained/AOT publishing. The Generic
Host remains the lifetime and dependency-injection boundary already selected in
[`runtime.md`](runtime.md).

## Rejected alternatives

- EF `HasData` and migration inserts conflate example content with schema state,
  bypass authorization, events, auditing, and public module behavior.
- A synthetic-data library changes the canonical teaching dataset and adds a
  dependency for no useful variability.
- A globally atomic seed transaction would hide the reference's observable
  partial-progress and failed-command audit semantics.
- Upsert-by-name would make restarts convenient but would not be a semantic port.

## Sources

- [.NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
- [Dependency injection in a console application](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/usage)
- [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
- [MSBuild embedded resources](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items)
