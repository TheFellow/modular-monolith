using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Mixology.Application.Operations;
using Mixology.Kernel.Errors;
using Mixology.Persistence;

namespace Mixology.Application.Auditing;

public sealed partial class TrackActivityMiddleware(
    MixologyStore store,
    IActivityRecorder recorder,
    TimeProvider timeProvider,
    ILogger<TrackActivityMiddleware> logger)
{
    public async Task InvokeAsync(OperationContext context, Operation operation, OperationDelegate next)
    {
        if (operation.Kind != OperationKind.Command)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        OperationActivity activity = new(operation, context.Principal, timeProvider.GetUtcNow());
        context.StartActivity(activity);
        ExceptionDispatchInfo? failure = null;
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }

        if (activity.CompletedAt is not null)
        {
            if (failure is not null)
            {
                failure.Throw();
            }

            return;
        }

        activity.Complete(context.TouchedEntities, failure?.SourceException, timeProvider.GetUtcNow());
        try
        {
            if (context.Session is { HasTransaction: true })
            {
                await recorder.RecordAsync(context, activity).ConfigureAwait(false);
            }
            else
            {
                await RecordSeparatelyAsync(context, activity).ConfigureAwait(false);
            }
        }
        catch (Exception recorderFailure)
        {
            FailedActivityRecording(logger, recorderFailure);
            if (failure is null)
            {
                throw AppError.Internal("record activity", recorderFailure);
            }
        }

        if (failure is not null)
        {
            failure.Throw();
        }
    }

    private async Task RecordSeparatelyAsync(OperationContext context, OperationActivity activity)
    {
        await using StoreSession session = await store.OpenSessionAsync(context.CancellationToken).ConfigureAwait(false);
        await session.BeginWriteAsync(context.CancellationToken).ConfigureAwait(false);
        try
        {
            await recorder.RecordAsync(context.WithSession(session), activity).ConfigureAwait(false);
            await session.Context.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
            await session.CommitAsync(context.CancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await session.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    [LoggerMessage(EventId = 10, Level = LogLevel.Error, Message = "Failed to record command activity")]
    private static partial void FailedActivityRecording(ILogger logger, Exception exception);
}

public sealed class RecordSuccessfulActivityMiddleware(
    IActivityRecorder recorder,
    TimeProvider timeProvider)
{
    public async Task InvokeAsync(OperationContext context, Operation operation, OperationDelegate next)
    {
        await next(context).ConfigureAwait(false);
        if (operation.Kind != OperationKind.Command)
        {
            return;
        }

        OperationActivity activity = context.Activity
            ?? throw AppError.Internal("activity missing from command context");
        activity.Complete(context.TouchedEntities, null, timeProvider.GetUtcNow());
        try
        {
            await recorder.RecordAsync(context, activity).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw AppError.Internal("record activity", exception);
        }
    }
}
