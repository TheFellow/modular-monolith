using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Mixology.Application.Operations;

public sealed class OperationMetrics : IDisposable
{
    private readonly Meter meter = new("Mixology.Application");

    internal Counter<long> CommandTotal { get; }
    internal Histogram<double> CommandDuration { get; }
    internal Counter<long> CommandErrors { get; }
    internal Counter<long> QueryTotal { get; }
    internal Histogram<double> QueryDuration { get; }
    internal Counter<long> QueryErrors { get; }

    public OperationMetrics()
    {
        CommandTotal = meter.CreateCounter<long>("mixology.command.total");
        CommandDuration = meter.CreateHistogram<double>("mixology.command.duration", "s");
        CommandErrors = meter.CreateCounter<long>("mixology.command.errors");
        QueryTotal = meter.CreateCounter<long>("mixology.query.total");
        QueryDuration = meter.CreateHistogram<double>("mixology.query.duration", "s");
        QueryErrors = meter.CreateCounter<long>("mixology.query.errors");
    }

    public void Dispose() => meter.Dispose();
}

public sealed class MetricsMiddleware(OperationMetrics metrics, TimeProvider timeProvider)
{
    public async Task InvokeAsync(OperationContext context, Operation operation, OperationDelegate next)
    {
        long started = timeProvider.GetTimestamp();
        string result = "success";
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch
        {
            result = "error";
            RecordError(operation);
            throw;
        }
        finally
        {
            TagList tags = new()
            {
                { "mixology.action", operation.Action },
                { "mixology.result", result },
            };
            double seconds = timeProvider.GetElapsedTime(started).TotalSeconds;
            if (operation.Kind == OperationKind.Command)
            {
                metrics.CommandTotal.Add(1, tags);
                metrics.CommandDuration.Record(seconds, tags);
            }
            else
            {
                metrics.QueryTotal.Add(1, tags);
                metrics.QueryDuration.Record(seconds, tags);
            }
        }
    }

    private void RecordError(Operation operation)
    {
        KeyValuePair<string, object?> action = new("mixology.action", operation.Action);
        if (operation.Kind == OperationKind.Command)
        {
            metrics.CommandErrors.Add(1, action);
        }
        else
        {
            metrics.QueryErrors.Add(1, action);
        }
    }
}
