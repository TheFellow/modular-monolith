using Microsoft.EntityFrameworkCore;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Persistence;
using Mixology.Persistence;

namespace Mixology.Modules.Drinks.Queries;

/// <summary>
/// Owner-defined drink reads for collaborating domains that already own a store session.
/// These queries do not re-enter application middleware or make an authorization decision.
/// </summary>
public sealed class DrinkQueries
{
    public async Task<Drink> GetAsync(
        StoreSession session,
        DrinkId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        try
        {
            DrinkRow? row = await DrinkRows(session.Context)
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.Id == id.Value && row.DeletedAtUtc == null,
                    cancellationToken)
                .ConfigureAwait(false);
            return row is null
                ? throw AppError.NotFound($"drink {id} not found")
                : FromRow(row);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read drink", exception);
        }
    }

    private static IQueryable<DrinkRow> DrinkRows(MixologyDbContext database) =>
        database.Set<DrinkRow>()
            .Include(row => row.RecipeIngredients)
            .ThenInclude(row => row.Substitutes)
            .Include(row => row.RecipeSteps)
            .AsSplitQuery();

    private static Drink FromRow(DrinkRow row)
    {
        try
        {
            Recipe recipe = new(
                row.RecipeIngredients.OrderBy(static value => value.Position).Select(value =>
                    new RecipeIngredient(
                        IngredientId.Parse(value.IngredientId),
                        Amount.Create(value.Amount, Unit.Parse(value.Unit)),
                        value.Optional,
                        value.Substitutes.OrderBy(static substitute => substitute.Position)
                            .Select(static substitute => IngredientId.Parse(substitute.SubstituteId)))),
                row.RecipeSteps.OrderBy(static value => value.Position).Select(static value => value.Value),
                row.Garnish);
            return new Drink(
                DrinkId.Parse(row.Id),
                row.Name,
                DrinkCategory.Parse(row.Category),
                GlassType.Parse(row.Glass),
                recipe,
                row.Description,
                DrinkStatus.Parse(row.Status),
                row.DeletedAtUtc is { } deletedAt
                    ? new DateTimeOffset(DateTime.SpecifyKind(deletedAt, DateTimeKind.Utc))
                    : null,
                TagCollection.Empty).Normalize();
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted drink {row.Id}", exception);
        }
    }

    private static void RequireId(DrinkId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("drink id is required");
        }

        _ = DrinkId.Parse(id.Value);
    }
}
