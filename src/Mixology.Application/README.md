# Application kernel

`Mixology.Application` supplies the host- and domain-neutral mechanics that make
the modules one application. It owns actor sessions, operation context,
middleware composition, audit activity, telemetry instruments, and event
contracts. It does not contain a module's business rules or a surface's view
models.

`MixologyHost.CreateBuilder` establishes the standard .NET Generic Host shape.
Composition roots then add persistence, application services, modules,
authorization, presentation projections, and their surface-specific host.
`MixologySessionFactory` binds an `Actor` and application cancellation token to
that graph; each invocation receives a fresh operation context.

## Operations

Commands and queries are described by an `Operation` with a stable action and
kind, then executed through `MixologySession`. The pipeline, transaction, and
audit contract is documented in [Operations](Operations/README.md).

`ExecuteAtomicAsync` is the deliberate higher-level composition seam. It lets a
mutation and continuation share one caller-owned transaction, with an EF flush
between stages. Ordinary module calls should use `ExecuteAsync` and let command
middleware own the unit of work.

## Events and audit

Commands enqueue domain events in their operation context. After the command
body succeeds, dispatcher middleware invokes the generated leaf routes inside
the same transaction. Activity middleware records the actor, action, direct
resource, and every indirect touch; see [Auditing](Auditing/README.md) and the
[dispatcher guide](../Mixology.Dispatcher/README.md).

## Telemetry

Application telemetry uses standard `Microsoft.Extensions.Logging` and
`System.Diagnostics.Metrics` APIs. The surface host selects Serilog sinks and
OpenTelemetry exporters; application/domain code records structured properties
and instruments without knowing whether diagnostics go to stderr, JSON, a
file, or Prometheus.

`OperationMetrics` emits:

| Instrument | Meaning |
| --- | --- |
| `mixology.command.total` | completed command attempts |
| `mixology.command.duration` | command latency in seconds |
| `mixology.command.errors` | failed commands |
| `mixology.query.total` | completed query attempts |
| `mixology.query.duration` | query latency in seconds |
| `mixology.query.errors` | failed queries |

Counters and durations carry low-cardinality `mixology.action` and result tags.
Do not attach entity IDs, names, filter text, or exception messages to metrics.
The CLI, TUI, and Desktop own the optional `localhost:9090/metrics` listener for
their complete host lifetime and dispose it during shutdown.
