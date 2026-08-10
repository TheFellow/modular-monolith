# Command-line interface

Status: Accepted  
Date: 2026-08-09

## Decision

Use stable System.CommandLine 2.0.10 for parsing and invocation, Spectre.Console
for human tables/details only, and System.Text.Json for machine output. Compose
the Generic Host manually after parsing. Do not use the discontinued
`System.CommandLine.Hosting` integration or Spectre.Console.Cli's parallel
application and DI model.

This fits the reference's nested typed commands, global database/actor/logging
options, validation, help-only filter schemas, templates, file-or-stdin JSON,
separate stdout/stderr, explicit exit codes, and one fresh operation context per
invocation. Redirected output and `NO_COLOR` disable decoration. JSON bypasses
Spectre completely.

Tests invoke the in-memory command tree with separate writers. Subprocess tests
remain mandatory for real exit status, database lifetime, and cross-surface
durability.

## Sources

- [System.CommandLine overview](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)
- [Parser and invocation configuration](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-configure-the-parser)
- [System.CommandLine repository and MIT license](https://github.com/dotnet/command-line-api)
- [System.CommandLine extension direction](https://github.com/dotnet/command-line-api/issues/2576)
- [Spectre.Console](https://spectreconsole.net/)
- [Testing Spectre output](https://spectreconsole.net/console/how-to/testing-console-output/)

