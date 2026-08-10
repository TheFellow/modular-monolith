using Mixology.Application.Authentication;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;

namespace Mixology.Application.Auditing;

public sealed class OperationActivity
{
    internal OperationActivity(Operation operation, Actor principal, DateTimeOffset startedAt)
    {
        Operation = operation;
        Principal = principal;
        StartedAt = startedAt;
    }

    public Operation Operation { get; }
    public Actor Principal { get; }
    public EntityUid? Resource { get; internal set; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public IReadOnlyList<EntityUid> Touches { get; private set; } = [];
    public bool Success { get; private set; }
    public ErrorKind? ErrorKind { get; private set; }
    public string? Error { get; private set; }

    internal bool RecordingFailed { get; private set; }

    internal void Complete(
        IReadOnlyList<EntityUid> touches,
        Exception? exception,
        DateTimeOffset completedAt)
    {
        if (CompletedAt is not null)
        {
            return;
        }

        Touches = touches.ToArray();
        Resource ??= Touches.Count > 0 ? Touches[0] : null;
        CompletedAt = completedAt;
        Success = exception is null;
        ErrorKind = AppError.Find(exception)?.Kind;
        Error = exception?.Message;
    }

    internal void Fail(
        IReadOnlyList<EntityUid> touches,
        Exception exception,
        DateTimeOffset completedAt)
    {
        Touches = touches.ToArray();
        Resource ??= Touches.Count > 0 ? Touches[0] : null;
        CompletedAt = completedAt;
        Success = false;
        ErrorKind = AppError.Find(exception)?.Kind;
        Error = exception.Message;
    }

    internal void MarkRecordingFailed() => RecordingFailed = true;
}

public interface IActivityRecorder
{
    Task RecordAsync(OperationContext context, OperationActivity activity);
}

internal sealed class MissingActivityRecorder : IActivityRecorder
{
    public Task RecordAsync(OperationContext context, OperationActivity activity)
    {
        _ = context;
        _ = activity;
        throw AppError.Internal("activity recorder is not registered");
    }
}
