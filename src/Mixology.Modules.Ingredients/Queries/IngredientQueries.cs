using Microsoft.EntityFrameworkCore;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Tags;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Persistence;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence;

namespace Mixology.Modules.Ingredients.Queries;

/// <summary>
/// Owner-defined queries available to collaborating domains inside an existing
/// store session. These queries deliberately do not re-enter the application
/// middleware or perform a second authorization decision.
/// </summary>
public sealed class IngredientQueries(ITagReader tags)
{
    public async Task<Ingredient> GetAsync(
        StoreSession session,
        IngredientId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        try
        {
            IngredientRow? row = await session.Context.Set<IngredientRow>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.Id == id.Value && row.DeletedAtUtc == null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                throw AppError.NotFound($"ingredient {id} not found");
            }

            Ingredient ingredient = FromRow(row);
            return ingredient with
            {
                Tags = await tags.ListAsync(
                    session.Context,
                    ingredient.EntityUid,
                    cancellationToken).ConfigureAwait(false),
            };
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read ingredient", exception);
        }
    }

    public async Task<IReadOnlyList<SubstitutionRule>> SubstitutionsForAsync(
        StoreSession session,
        IngredientId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        try
        {
            IngredientRow[] rows = await session.Context.Set<IngredientRow>()
                .AsNoTracking()
                .Where(static row => row.DeletedAtUtc == null)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            return IngredientSubstitutionCatalog.Resolve(id, rows.Select(FromRow));
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("resolve ingredient substitutions", exception);
        }
    }

    public async Task<IReadOnlySet<string>> ActiveIdsAsync(
        StoreSession session,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(ids);
        IngredientId[] requested = ids.Distinct(StringComparer.Ordinal).Select(IngredientId.Parse).ToArray();
        string[] values = requested.Select(static value => value.Value).ToArray();
        try
        {
            string[] active = await session.Context.Set<IngredientRow>()
                .AsNoTracking()
                .Where(row => values.Contains(row.Id) && row.DeletedAtUtc == null)
                .Select(static row => row.Id)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            return active.ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read active ingredient ids", exception);
        }
    }

    public async Task RequireActiveAsync(
        StoreSession session,
        IngredientId id,
        CancellationToken cancellationToken = default)
    {
        _ = await GetAsync(session, id, cancellationToken).ConfigureAwait(false);
    }

    private static Ingredient FromRow(IngredientRow row)
    {
        try
        {
            return new Ingredient(
                IngredientId.Parse(row.Id),
                row.Name,
                IngredientCategory.Parse(row.Category),
                Unit.Parse(row.Unit),
                row.Description,
                row.DeletedAtUtc is { } deletedAt
                    ? new DateTimeOffset(DateTime.SpecifyKind(deletedAt, DateTimeKind.Utc))
                    : null,
                TagCollection.Empty,
                row.Revision).Normalize();
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted ingredient {row.Id}", exception);
        }
    }

    private static void RequireId(IngredientId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("ingredient id is required");
        }

        _ = IngredientId.Parse(id.Value);
    }
}
