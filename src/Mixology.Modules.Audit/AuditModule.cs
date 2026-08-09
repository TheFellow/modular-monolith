using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Operations;
using Mixology.Authorization.Cedar;
using Mixology.Filtering;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Modules.Audit.Authorization;
using Mixology.Modules.Audit.Models;
using Mixology.Modules.Audit.Persistence;
using Mixology.Modules.Audit.Requests;
using Mixology.Persistence;

namespace Mixology.Modules.Audit;

public sealed class AuditModule(
    MixologyStore store,
    IEntityAuthorizer authorizer)
{
    public Task<Page<AuditEntry>> ListAsync(
        MixologySession session,
        ListAuditEntriesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ListAuditEntriesRequest normalized = request.Normalize();
        FilterExpression<AuditFilter>? expression = Filter.Parse(AuditFilter.Schema, normalized.Filter);
        return session.ExecuteAsync(
            Query(AuditAuthorization.List),
            context => ListCoreAsync(context, normalized, expression),
            cancellationToken);
    }

    public async Task<int> CountAsync(
        MixologySession session,
        ListAuditEntriesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ListAuditEntriesRequest normalized = request.Normalize() with
        {
            Cursor = default,
            Limit = PageRequest.DefaultLimit,
        };
        return await Paging.CountAsync<AuditEntry>(
            async (cursor, token) => await ListAsync(
                session,
                normalized with { Cursor = cursor },
                token).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<Page<AuditEntry>> GetEntityHistoryAsync(
        MixologySession session,
        EntityUid entity,
        CancellationToken cancellationToken = default)
    {
        RequireUid(entity, "entity");
        return ListAsync(
            session,
            new ListAuditEntriesRequest(Entity: entity),
            cancellationToken);
    }

    public Task<Page<AuditEntry>> GetActorActivityAsync(
        MixologySession session,
        Actor principal,
        CancellationToken cancellationToken = default)
    {
        if (principal.IsEmpty)
        {
            throw AppError.Invalid("principal is required");
        }

        return ListAsync(
            session,
            new ListAuditEntriesRequest(Principal: principal),
            cancellationToken);
    }

    private async Task<Page<AuditEntry>> ListCoreAsync(
        OperationContext context,
        ListAuditEntriesRequest request,
        FilterExpression<AuditFilter>? expression)
    {
        AuditEntryRow[] candidates = await ReadAsync(
            async database =>
            {
                IQueryable<AuditEntryRow> query = database.Set<AuditEntryRow>()
                    .AsNoTracking()
                    .Include(static row => row.Touches);

                if (IsSpecified(request.Action))
                {
                    string action = CedarName(request.Action);
                    query = query.Where(row => row.Action == action);
                }

                if (request.Principal is { } principal)
                {
                    string principalId = principal.Id;
                    query = query.Where(row => row.PrincipalId == principalId);
                }

                if (IsSpecified(request.Entity))
                {
                    string entityType = request.Entity.Type;
                    string entityId = request.Entity.Id;
                    query = query.Where(row =>
                        (row.ResourceType == entityType && row.ResourceId == entityId)
                        || row.Touches.Any(touch =>
                            touch.EntityType == entityType && touch.EntityId == entityId));
                }

                if (request.From is { } from)
                {
                    DateTime fromUtc = from.UtcDateTime;
                    query = query.Where(row => row.StartedAtUtc >= fromUtc);
                }

                if (request.To is { } to)
                {
                    DateTime toUtc = to.UtcDateTime;
                    query = query.Where(row => row.StartedAtUtc <= toUtc);
                }

                Expression<Func<AuditEntryRow, bool>>? pushdown = expression?.BuildPushdown(AuditFilter.Persistence);
                if (pushdown is not null)
                {
                    query = query.Where(pushdown);
                }

                return await query.OrderByDescending(static row => row.Id)
                    .ToArrayAsync(context.CancellationToken)
                    .ConfigureAwait(false);
            },
            context.CancellationToken).ConfigureAwait(false);

        if (!request.Cursor.IsEmpty)
        {
            candidates = candidates
                .Where(row => string.CompareOrdinal(row.Id, request.Cursor.Value) < 0)
                .ToArray();
        }

        List<AuditEntry> visible = [];
        foreach (AuditEntryRow row in candidates)
        {
            AuditEntry entry = FromRow(row);
            if (expression is not null && !expression.Match(ToFilter(entry)))
            {
                continue;
            }

            try
            {
                await authorizer.AuthorizeAsync(
                    context.Principal,
                    AuditAuthorization.List,
                    entry.ToCedarEntity(),
                    context.CancellationToken).ConfigureAwait(false);
            }
            catch (PermissionError)
            {
                continue;
            }

            visible.Add(entry);
            if (visible.Count > request.Limit)
            {
                break;
            }
        }

        bool hasNext = visible.Count > request.Limit;
        if (hasNext)
        {
            visible.RemoveAt(visible.Count - 1);
        }

        Cursor next = hasNext ? new Cursor(visible[^1].Id.Value) : default;
        return new Page<AuditEntry>(visible, next);
    }

    private async Task<TResult> ReadAsync<TResult>(
        Func<MixologyDbContext, Task<TResult>> query,
        CancellationToken cancellationToken)
    {
        try
        {
            await using StoreSession read = await store.OpenSessionAsync(cancellationToken).ConfigureAwait(false);
            return await query(read.Context).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not AppError and not OperationCanceledException)
        {
            throw AppError.Internal("read audit entries", exception);
        }
    }

    private static AuditEntry FromRow(AuditEntryRow row)
    {
        try
        {
            AuditEntryId id = AuditEntryId.Parse(row.Id);
            Actor principal = Actor.Parse(row.PrincipalId);
            if (!string.Equals(principal.Id, row.PrincipalId, StringComparison.Ordinal))
            {
                throw AppError.Invalid("persisted principal is not canonical");
            }

            EntityUid? resource = (row.ResourceType, row.ResourceId) switch
            {
                (null, null) => null,
                ({ } type, { } resourceId) when type.Length > 0 && resourceId.Length > 0 => new EntityUid(type, resourceId),
                _ => throw AppError.Invalid("persisted resource type and id must be provided together"),
            };

            ErrorKind? errorKind = row.ErrorKind is { } rawKind
                ? Enum.IsDefined((ErrorKind)rawKind)
                    ? (ErrorKind)rawKind
                    : throw AppError.Invalid("persisted error kind is invalid")
                : null;
            EntityUid[] touches = row.Touches
                .OrderBy(static touch => touch.Position)
                .Select(static touch => new EntityUid(touch.EntityType, touch.EntityId))
                .ToArray();
            return new AuditEntry(
                id,
                row.Action,
                resource,
                principal,
                Utc(row.StartedAtUtc),
                Utc(row.CompletedAtUtc),
                row.Success,
                errorKind,
                row.Error,
                touches);
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted audit entry {row.Id}", exception);
        }
    }

    private static AuditFilter ToFilter(AuditEntry entry) => new(
        entry.Id.Value,
        entry.Action,
        entry.Resource is { } resource ? CedarName(resource) : string.Empty,
        CedarName(new EntityUid(CedarMappings.ActorType, entry.Principal.Id)),
        entry.StartedAt,
        entry.CompletedAt,
        entry.Success,
        entry.Error ?? string.Empty);

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static bool IsSpecified(EntityUid uid) =>
        !string.IsNullOrEmpty(uid.Type) && !string.IsNullOrEmpty(uid.Id);

    private static void RequireUid(EntityUid uid, string name)
    {
        if (!IsSpecified(uid))
        {
            throw AppError.Invalid($"{name} type and id are required");
        }
    }

    private static string CedarName(EntityUid uid) => uid.ToCedarUid().MarshalCedar();
    private static Operation Query(EntityUid action) => Operation.Query(CedarName(action));
}
