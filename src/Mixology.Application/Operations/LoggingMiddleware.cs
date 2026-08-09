using Microsoft.Extensions.Logging;
using Mixology.Kernel.Errors;

namespace Mixology.Application.Operations;

public sealed partial class LoggingMiddleware(ILogger<LoggingMiddleware> logger, TimeProvider timeProvider)
{
    public async Task InvokeAsync(OperationContext context, Operation operation, OperationDelegate next)
    {
        long started = timeProvider.GetTimestamp();
        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Actor"] = context.Principal.Id,
            ["Action"] = operation.Action,
            ["OperationKind"] = operation.Kind.ToString(),
        });
        OperationStarted(logger, operation.Kind);

        try
        {
            await next(context).ConfigureAwait(false);
            TimeSpan duration = timeProvider.GetElapsedTime(started);
            if (operation.Kind == OperationKind.Command)
            {
                CommandCompleted(logger, duration);
            }
            else
            {
                QueryCompleted(logger, duration);
            }
        }
        catch (Exception exception)
        {
            TimeSpan duration = timeProvider.GetElapsedTime(started);
            if (AppError.IsPermission(exception))
            {
                OperationDenied(logger, operation.Kind, duration, exception);
            }
            else if (operation.Kind == OperationKind.Command)
            {
                CommandFailed(logger, duration, exception);
            }
            else
            {
                QueryFailed(logger, duration, exception);
            }

            throw;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "{OperationKind} started")]
    private static partial void OperationStarted(ILogger logger, OperationKind operationKind);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Command completed in {Duration}")]
    private static partial void CommandCompleted(ILogger logger, TimeSpan duration);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Query completed in {Duration}")]
    private static partial void QueryCompleted(ILogger logger, TimeSpan duration);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "{OperationKind} denied in {Duration}")]
    private static partial void OperationDenied(
        ILogger logger,
        OperationKind operationKind,
        TimeSpan duration,
        Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Command failed in {Duration}")]
    private static partial void CommandFailed(ILogger logger, TimeSpan duration, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Query failed in {Duration}")]
    private static partial void QueryFailed(ILogger logger, TimeSpan duration, Exception exception);
}
