using Mixology.Application.Authentication;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;

namespace Mixology.Modules.Audit.Requests;

public sealed record ListAuditEntriesRequest(
    EntityUid Action = default,
    Actor? Principal = null,
    EntityUid Entity = default,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Filter = null,
    Cursor Cursor = default,
    int Limit = 0)
{
    public int EffectiveLimit => Limit == 0 ? PageRequest.DefaultLimit : Limit;

    public ListAuditEntriesRequest Normalize()
    {
        ValidateOptionalUid(Action, "action");
        ValidateOptionalUid(Entity, "entity");
        if (Principal is { IsEmpty: true })
        {
            throw AppError.Invalid("principal is required when filtering by principal");
        }

        if (Limit < 0)
        {
            throw AppError.Invalid("page limit must be greater than zero");
        }

        if (!Cursor.IsEmpty)
        {
            _ = AuditEntryId.Parse(Cursor.Value);
        }

        DateTimeOffset? from = From?.ToUniversalTime();
        DateTimeOffset? to = To?.ToUniversalTime();
        if (from > to)
        {
            throw AppError.Invalid("audit start time must not be after end time");
        }

        return this with
        {
            From = from,
            To = to,
            Filter = Filter?.Trim(),
            Limit = EffectiveLimit,
        };
    }

    public void Validate() => _ = Normalize();

    private static void ValidateOptionalUid(EntityUid uid, string name)
    {
        bool hasType = !string.IsNullOrEmpty(uid.Type);
        bool hasId = !string.IsNullOrEmpty(uid.Id);
        if (hasType != hasId)
        {
            throw AppError.Invalid($"{name} type and id must be provided together");
        }
    }
}
