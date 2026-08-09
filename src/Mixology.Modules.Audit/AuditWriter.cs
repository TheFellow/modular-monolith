using Mixology.Application.Auditing;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Modules.Audit.Persistence;

namespace Mixology.Modules.Audit;

public sealed class AuditWriter : IActivityRecorder
{
    public Task RecordAsync(OperationContext context, OperationActivity activity)
    {
        if (context.Session is not { HasTransaction: true } session)
        {
            throw AppError.Internal("recording activity requires an active transaction");
        }

        DateTimeOffset completedAt = activity.CompletedAt
            ?? throw AppError.Internal("cannot record incomplete activity");
        AuditEntryId id = AuditEntryId.New();
        AuditEntryRow row = new()
        {
            Id = id.Value,
            Action = activity.Operation.Action,
            ResourceType = activity.Resource?.Type,
            ResourceId = activity.Resource?.Id,
            PrincipalId = activity.Principal.Id,
            StartedAtUtc = activity.StartedAt.UtcDateTime,
            CompletedAtUtc = completedAt.UtcDateTime,
            Success = activity.Success,
            ErrorKind = activity.ErrorKind is { } kind ? (int)kind : null,
            Error = activity.Error,
            Touches = activity.Touches.Select((touch, position) => new AuditTouchRow
            {
                AuditEntryId = id.Value,
                Position = position,
                EntityType = touch.Type,
                EntityId = touch.Id,
            }).ToList(),
        };
        session.Context.Add(row);
        return Task.CompletedTask;
    }
}
