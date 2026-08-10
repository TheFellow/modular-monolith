# Runtime and application host

Status: Accepted  
Date: 2026-08-09

## Decision

Target .NET 10 LTS and C# 14. Use `Host.CreateApplicationBuilder` as the common
composition shape for CLI, TUI, desktop, and seed executables. Central package
management, SDK analyzers, configured code-style analysis, nullable references,
deterministic builds, and warnings-as-errors are enabled from the first commit.

The repository does not opt into a .NET 11 preview merely to experiment with
proposed native discriminated unions. Closed domain alternatives use explicit
abstract record hierarchies and exhaustive pattern matching until the feature is
available in the pinned stable SDK.

The Generic Host supplies DI, configuration, logging, application lifetime, and
graceful shutdown without pulling business behavior into an executable. Each
surface owns native interaction state while resolving the same application
boundary. CLI parsing is deliberately outside startup so `--help` and
`--filter-help` do not open the database.

## Validation

- The solution builds and tests on the pinned SDK with no warnings.
- Formatting is stable under `dotnet format --verify-no-changes`.
- Every executable composes through the same registration extensions.
- Help-only CLI paths do not build or start the application host.

## Sources

- [.NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
- [Dependency injection in console applications](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/usage)
- [SDK analyzer and code-style build properties](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props)
- [EditorConfig code-style rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options)

