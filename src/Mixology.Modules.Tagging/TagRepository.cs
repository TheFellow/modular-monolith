using Microsoft.EntityFrameworkCore;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Tags;
using Mixology.Modules.Tagging.Models;
using Mixology.Modules.Tagging.Persistence;
using Mixology.Persistence;

namespace Mixology.Modules.Tagging;

public sealed class TagRepository : ITagReader
{
    public Task<TagCollection> ListAsync(
        MixologyDbContext database,
        EntityUid target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ValidateTarget(target);
        return ListCoreAsync(database, target, cancellationToken);
    }

    public async Task<TagCollection> ListAsync(
        StoreSession session,
        EntityUid target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ValidateTarget(target);
        return await ListCoreAsync(session.Context, target, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TagCollection> ListCoreAsync(
        MixologyDbContext database,
        EntityUid target,
        CancellationToken cancellationToken)
    {
        TagAssociationRow[] rows = await database.Set<TagAssociationRow>()
            .AsNoTracking()
            .Where(row => row.EntityType == target.Type && row.EntityId == target.Id)
            .OrderBy(row => row.Key)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return ToTags(rows);
    }

    public Task<IReadOnlyDictionary<EntityUid, TagCollection>> ListTypeAsync(
        MixologyDbContext database,
        string entityType,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        return ListTypeCoreAsync(database, entityType, ids, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<EntityUid, TagCollection>> ListTypeAsync(
        StoreSession session,
        string entityType,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return await ListTypeCoreAsync(session.Context, entityType, ids, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyDictionary<EntityUid, TagCollection>> ListTypeCoreAsync(
        MixologyDbContext database,
        string entityType,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return new Dictionary<EntityUid, TagCollection>();
        }

        string[] distinct = ids.Distinct(StringComparer.Ordinal).ToArray();
        foreach (string id in distinct)
        {
            ValidateTarget(new EntityUid(entityType, id));
        }

        TagAssociationRow[] rows = await database.Set<TagAssociationRow>()
            .AsNoTracking()
            .Where(row => row.EntityType == entityType && distinct.Contains(row.EntityId))
            .OrderBy(row => row.EntityId)
            .ThenBy(row => row.Key)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.GroupBy(row => new EntityUid(row.EntityType, row.EntityId))
            .ToDictionary(
                group => group.Key,
                group => ToTags(group),
                EqualityComparer<EntityUid>.Default);
    }

    internal async Task<bool> UpsertAsync(
        StoreSession session,
        EntityUid target,
        Tag value,
        CancellationToken cancellationToken)
    {
        ValidateTarget(target);
        value.Validate();
        TagAssociationRow? row = await session.Context.Set<TagAssociationRow>()
            .SingleOrDefaultAsync(
                row => row.EntityType == target.Type && row.EntityId == target.Id && row.Key == value.Key,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            session.Context.Add(new TagAssociationRow
            {
                EntityType = target.Type,
                EntityId = target.Id,
                Key = value.Key,
                Value = value.Value,
            });
            return true;
        }

        if (string.Equals(row.Value, value.Value, StringComparison.Ordinal))
        {
            return false;
        }

        row.Value = value.Value;
        return true;
    }

    internal async Task<bool> ReplaceAsync(
        StoreSession session,
        EntityUid target,
        TagCollection desired,
        CancellationToken cancellationToken)
    {
        ValidateTarget(target);
        ArgumentNullException.ThrowIfNull(desired);
        desired.Validate();
        TagAssociationRow[] existing = await session.Context.Set<TagAssociationRow>()
            .Where(row => row.EntityType == target.Type && row.EntityId == target.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, TagAssociationRow> byKey = existing.ToDictionary(row => row.Key, StringComparer.Ordinal);
        bool changed = false;
        foreach (Tag value in desired)
        {
            if (!byKey.Remove(value.Key, out TagAssociationRow? row))
            {
                session.Context.Add(new TagAssociationRow
                {
                    EntityType = target.Type,
                    EntityId = target.Id,
                    Key = value.Key,
                    Value = value.Value,
                });
                changed = true;
            }
            else if (!string.Equals(row.Value, value.Value, StringComparison.Ordinal))
            {
                row.Value = value.Value;
                changed = true;
            }
        }

        if (byKey.Count != 0)
        {
            session.Context.RemoveRange(byKey.Values);
            changed = true;
        }

        return changed;
    }

    internal async Task<bool> RemoveAsync(
        StoreSession session,
        EntityUid target,
        string key,
        CancellationToken cancellationToken)
    {
        ValidateTarget(target);
        new Tag(key).Validate();
        TagAssociationRow? row = await session.Context.Set<TagAssociationRow>()
            .SingleOrDefaultAsync(
                row => row.EntityType == target.Type && row.EntityId == target.Id && row.Key == key,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        session.Context.Remove(row);
        return true;
    }

    public async Task<int> DeleteTargetAsync(
        StoreSession session,
        EntityUid target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ValidateTarget(target);
        return await session.Context.Set<TagAssociationRow>()
            .Where(row => row.EntityType == target.Type && row.EntityId == target.Id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task<TagAssociation[]> FindAsync(
        StoreSession session,
        Tag value,
        bool exact,
        CancellationToken cancellationToken)
    {
        value.Validate();
        IQueryable<TagAssociationRow> query = session.Context.Set<TagAssociationRow>()
            .AsNoTracking()
            .Where(row => row.Key == value.Key);
        if (exact)
        {
            query = query.Where(row => row.Value == value.Value);
        }

        return await query.OrderBy(row => row.EntityType)
            .ThenBy(row => row.EntityId)
            .Select(row => new TagAssociation(
                new EntityUid(row.EntityType, row.EntityId),
                new Tag(row.Key, row.Value)))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task<TagAssociation[]> AllAsync(
        StoreSession session,
        CancellationToken cancellationToken) =>
        await session.Context.Set<TagAssociationRow>()
            .AsNoTracking()
            .OrderBy(row => row.Key)
            .ThenBy(row => row.Value)
            .ThenBy(row => row.EntityType)
            .ThenBy(row => row.EntityId)
            .Select(row => new TagAssociation(
                new EntityUid(row.EntityType, row.EntityId),
                new Tag(row.Key, row.Value)))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    internal static void ValidateTarget(EntityUid target)
    {
        if (target.Type is not (
            EntityIds.DrinkType or
            EntityIds.IngredientType or
            EntityIds.InventoryType or
            EntityIds.MenuType or
            EntityIds.OrderType))
        {
            throw AppError.Invalid($"unsupported tag target type: {target.Type}");
        }

        EntityUid parsed;
        try
        {
            parsed = EntityIds.Parse(target.Id);
        }
        catch (InvalidError exception)
        {
            throw AppError.Invalid($"invalid tag target {target.Type}::{target.Id}", exception);
        }

        if (!string.Equals(parsed.Type, target.Type, StringComparison.Ordinal))
        {
            throw AppError.Invalid($"invalid tag target {target.Type}::{target.Id}");
        }
    }

    private static TagCollection ToTags(IEnumerable<TagAssociationRow> rows) =>
        new(rows.Select(row => new Tag(row.Key, row.Value)));
}

internal sealed record TagAssociation(EntityUid Target, Tag Tag);
