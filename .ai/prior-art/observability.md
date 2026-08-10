# CLI observability

Status: Accepted
Date: 2026-08-09

## Inherited semantics

The Go CLI exposes process-global `--log-level`, `--log-format`, `--log-file`,
and `--metrics` flags. Logs default to informational text on stderr, a file
replaces stderr when selected, JSON is newline-delimited, and metrics expose the
application instruments at `localhost:9090/metrics` only for the invocation's
lifetime. Every invocation must construct and dispose fresh logging and metrics
state. Diagnostics must never enter command stdout, especially JSON output.

## Decision

Keep instrumentation in `System.Diagnostics.Metrics`; `OperationMetrics` remains
the single owner of the `Mixology.Application` meter. When `--metrics` is set,
the Generic Host starts OpenTelemetry's Prometheus HttpListener exporter for
that meter on loopback port 9090 and disposes it with the host. The exporter is
an inner-loop component, matching the reference CLI's local development
endpoint. It is not the future production export path; a long-running service
should use the stable OTLP exporter and an OpenTelemetry Collector.

Use `Microsoft.Extensions.Logging.ILogger` throughout application code and
Serilog only as the Generic Host provider at the executable edge. The official
host adapter routes framework and application `ILogger` events through the same
configuration. Its console sink is pinned to process stderr, while its file
sink supplies append, exclusive lifecycle disposal, text, and JSON output that
the built-in providers do not provide. Logging configuration is validated and
rebuilt for every invocation; no static logger is used.

The Desktop executable uses the same option values, validation, stderr/file
selection, formats, and Prometheus endpoint. Its Generic Host outlives the
Avalonia lifetime and is disposed after the shell drains, so background work
cannot write through a disposed provider. The small configuration record is
duplicated at each executable edge for now: extracting a shared observability
project during active CLI/TUI/Desktop composition work would couple their
different defaults and lifecycle adapters without removing domain logic. A
shared package becomes worthwhile only if another long-running process needs
the exact same edge policy.

Accepted levels are `debug`, `info`, `warn`/`warning`, and `error`; formats are
`text` and `json`. Invalid values and unusable log paths are typed invalid-input
errors rather than silently falling back. Environment parity uses
`MIXOLOGY_LOG_LEVEL`, `MIXOLOGY_LOG_FORMAT`, `MIXOLOGY_LOG_FILE`, and
`MIXOLOGY_METRICS`, with command-line values taking precedence.

## Rejected alternatives

- A custom file `ILoggerProvider` would duplicate mature formatting, append,
  sharing, and disposal behavior.
- A custom Prometheus renderer over `MeterListener` would duplicate an
  exporter protocol and histogram aggregation.
- The Prometheus ASP.NET Core exporter would add a web-host surface solely for
  a short-lived CLI endpoint.
- Writing diagnostics through the command output writer would corrupt JSON and
  pipelines; the process stderr/file boundary is intentional.

## Validation

- Text and JSON logs contain operation events only at or above the configured
  level and never appear in stdout.
- A log file can be reopened by a later invocation, proving host-owned disposal.
- Invalid level, format, metrics environment value, and log path use typed CLI
  error mappings.
- Consecutive metrics-enabled invocations can bind port 9090, proving exporter
  lifecycle isolation.
- Desktop headless tests prove text/JSON level filtering, exclusive file reopen,
  typed invalid destinations, environment defaults, and sequential exporter
  rebinding under its longer-lived host.

## Sources

- [.NET console log formatters](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/console-log-formatter)
- [.NET `MeterListener`](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meterlistener?view=net-10.0)
- [Serilog Generic Host integration](https://github.com/serilog/serilog-extensions-hosting)
- [Serilog file sink and JSON formatting](https://github.com/serilog/serilog-sinks-file)
- [OpenTelemetry .NET Generic Host lifecycle](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting/1.17.0)
- [OpenTelemetry Prometheus HttpListener exporter](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Prometheus.HttpListener/README.md)
