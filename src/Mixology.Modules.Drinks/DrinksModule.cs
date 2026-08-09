using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Mixology.Application;
using Mixology.Application.Operations;
using Mixology.Authorization.Cedar;
using Mixology.Filtering;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks.Authorization;
using Mixology.Modules.Drinks.Events;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Persistence;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients.Queries;
using Mixology.Persistence;

namespace Mixology.Modules.Drinks;

public sealed class DrinksModule(
    MixologyStore store,
    IngredientQueries ingredients,
    IEntityAuthorizer authorizer,
    TimeProvider timeProvider)
{
    public Task<Drink> CreateAsync(
        MixologySession session,
        CreateDrinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return session.ExecuteAsync(
            Command(DrinkAuthorization.Create),
            async context =>
            {
                CreateDrinkRequest normalized = request.Normalize();
                Drink created = new(
                    DrinkId.New(),
                    normalized.Name,
                    normalized.Category,
                    normalized.Glass,
                    normalized.Recipe,
                    normalized.Description,
                    DrinkStatus.Active,
                    null,
                    TagCollection.Empty);
                await AuthorizeAsync(context, DrinkAuthorization.Create, created).ConfigureAwait(false);
                await ValidateIngredientsAsync(context, created.Recipe).ConfigureAwait(false);
                context.Session!.Context.Add(ToRow(created));
                context.SelectResource(created.EntityUid);
                context.Touch(created.EntityUid);
                context.AddEvent(new DrinkCreated(created));
                return created;
            },
            cancellationToken);
    }

    public Task<Drink> GetAsync(
        MixologySession session,
        DrinkId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        return session.ExecuteAsync(
            Query(DrinkAuthorization.Get),
            async context =>
            {
                Drink drink = await ReadAsync(
                    async database =>
                    {
                        DrinkRow? row = await DrinkRows(database)
                            .SingleOrDefaultAsync(
                                row => row.Id == id.Value && row.DeletedAtUtc == null,
                                context.CancellationToken)
                            .ConfigureAwait(false);
                        return row is null
                            ? throw AppError.NotFound($"drink {id} not found")
                            : FromRow(row);
                    },
                    context.CancellationToken).ConfigureAwait(false);
                await AuthorizeAsync(context, DrinkAuthorization.Get, drink).ConfigureAwait(false);
                return drink;
            },
            cancellationToken);
    }

    public Task<Drink> UpdateAsync(
        MixologySession session,
        UpdateDrinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return session.ExecuteAsync(
            Command(DrinkAuthorization.Update),
            async context =>
            {
                UpdateDrinkRequest normalized = request.Normalize();
                DrinkRow row = await RequireActiveRowAsync(context, normalized.Id).ConfigureAwait(false);
                Drink current = FromRow(row);
                await AuthorizeAsync(context, DrinkAuthorization.Update, current).ConfigureAwait(false);
                Drink updated = new Drink(
                    normalized.Id,
                    normalized.Name,
                    normalized.Category,
                    normalized.Glass,
                    normalized.Recipe,
                    normalized.Description,
                    DrinkStatus.Active,
                    null,
                    current.Tags).Normalize();
                await AuthorizeAsync(context, DrinkAuthorization.Update, updated).ConfigureAwait(false);
                await ValidateIngredientsAsync(context, updated.Recipe).ConfigureAwait(false);
                CopyToRow(updated, row);
                context.SelectResource(updated.EntityUid);
                context.Touch(updated.EntityUid);
                context.AddEvent(new DrinkUpdated(updated));
                return updated;
            },
            cancellationToken);
    }

    public Task<Drink> DeleteAsync(
        MixologySession session,
        DrinkId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        return session.ExecuteAsync(
            Command(DrinkAuthorization.Delete),
            async context =>
            {
                DrinkRow row = await RequireActiveRowAsync(context, id).ConfigureAwait(false);
                Drink current = FromRow(row);
                await AuthorizeAsync(context, DrinkAuthorization.Delete, current).ConfigureAwait(false);
                DateTimeOffset deletedAt = timeProvider.GetUtcNow().ToUniversalTime();
                Drink deleted = current with { DeletedAt = deletedAt };
                await AuthorizeAsync(context, DrinkAuthorization.Delete, deleted).ConfigureAwait(false);
                row.DeletedAtUtc = deletedAt.UtcDateTime;
                context.SelectResource(deleted.EntityUid);
                context.Touch(deleted.EntityUid);
                context.AddEvent(new DrinkDeleted(deleted, deletedAt));
                return deleted;
            },
            cancellationToken);
    }

    public Task<Page<Drink>> ListAsync(
        MixologySession session,
        ListDrinksRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ListDrinksRequest normalized = request.Normalize();
        FilterExpression<DrinkFilter>? expression = Filter.Parse(DrinkFilter.Schema, normalized.Filter);
        return session.ExecuteAsync(
            Query(DrinkAuthorization.List),
            context => ListCoreAsync(context, normalized, expression),
            cancellationToken);
    }

    public async Task<int> CountAsync(
        MixologySession session,
        ListDrinksRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ListDrinksRequest normalized = request.Normalize() with
        {
            Cursor = default,
            Limit = PageRequest.DefaultLimit,
        };
        return await Paging.CountAsync<Drink>(
            async (cursor, token) => await ListAsync(session, normalized with { Cursor = cursor }, token)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<Drink>> ListByIngredientAsync(
        MixologySession session,
        IngredientId ingredientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireIngredientId(ingredientId);
        return session.ExecuteAsync(
            Query(DrinkAuthorization.List),
            async context =>
            {
                DrinkRow[] rows = await ReadAsync(
                    database => DrinkRows(database)
                        .Where(row => row.DeletedAtUtc == null && row.RecipeIngredients.Any(
                            recipe => recipe.IngredientId == ingredientId.Value ||
                                      recipe.Substitutes.Any(substitute => substitute.SubstituteId == ingredientId.Value)))
                        .OrderBy(row => row.Name)
                        .ToArrayAsync(context.CancellationToken),
                    context.CancellationToken).ConfigureAwait(false);
                List<Drink> visible = [];
                foreach (DrinkRow row in rows)
                {
                    Drink drink = FromRow(row);
                    try
                    {
                        await AuthorizeAsync(context, DrinkAuthorization.List, drink).ConfigureAwait(false);
                        visible.Add(drink);
                    }
                    catch (PermissionError)
                    {
                    }
                }

                return (IReadOnlyList<Drink>)visible;
            },
            cancellationToken);
    }

    public Task<IReadOnlySet<DrinkId>> ActiveIdsAsync(
        MixologySession session,
        IEnumerable<DrinkId> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(ids);
        DrinkId[] requested = ids.Distinct().ToArray();
        foreach (DrinkId id in requested)
        {
            RequireId(id);
        }

        return session.ExecuteAsync(
            Query(DrinkAuthorization.List),
            async context =>
            {
                string[] values = requested.Select(static id => id.Value).ToArray();
                string[] active = await ReadAsync(
                    database => database.Set<DrinkRow>()
                        .AsNoTracking()
                        .Where(row => values.Contains(row.Id) && row.DeletedAtUtc == null)
                        .Select(row => row.Id)
                        .ToArrayAsync(context.CancellationToken),
                    context.CancellationToken).ConfigureAwait(false);
                return (IReadOnlySet<DrinkId>)active.Select(DrinkId.Parse).ToHashSet();
            },
            cancellationToken);
    }

    private async Task<Page<Drink>> ListCoreAsync(
        OperationContext context,
        ListDrinksRequest request,
        FilterExpression<DrinkFilter>? expression)
    {
        DrinkRow[] candidates = await ReadAsync(
            async database =>
            {
                IQueryable<DrinkRow> query = DrinkRows(database).Where(static row => row.DeletedAtUtc == null);
                if (request.Name is { } name)
                {
                    query = query.Where(row => row.Name == name);
                }

                if (request.Category is { } category)
                {
                    string value = category.Value;
                    query = query.Where(row => row.Category == value);
                }

                if (request.Glass is { } glass)
                {
                    string value = glass.Value;
                    query = query.Where(row => row.Glass == value);
                }

                Expression<Func<DrinkRow, bool>>? pushdown = expression?.BuildPushdown(DrinkFilter.Persistence);
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
            candidates = candidates.Where(row => string.CompareOrdinal(row.Id, request.Cursor.Value) < 0).ToArray();
        }

        List<Drink> visible = [];
        foreach (DrinkRow row in candidates)
        {
            Drink drink = FromRow(row);
            if (expression is not null && !expression.Match(ToFilter(drink)))
            {
                continue;
            }

            try
            {
                await AuthorizeAsync(context, DrinkAuthorization.List, drink).ConfigureAwait(false);
            }
            catch (PermissionError)
            {
                continue;
            }

            visible.Add(drink);
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
        return new Page<Drink>(visible, next);
    }

    private async Task ValidateIngredientsAsync(OperationContext context, Recipe recipe)
    {
        StoreSession session = context.Session
            ?? throw AppError.Internal("drink ingredient validation requires an active store session");
        foreach (RecipeIngredient recipeIngredient in recipe.Ingredients)
        {
            try
            {
                await ingredients.RequireActiveAsync(session, recipeIngredient.IngredientId, context.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (NotFoundError exception) when (!recipeIngredient.Optional)
            {
                throw AppError.Invalid($"ingredient {recipeIngredient.IngredientId} not found", exception);
            }
            catch (NotFoundError) when (recipeIngredient.Optional)
            {
            }

            foreach (IngredientId substitute in recipeIngredient.Substitutes)
            {
                try
                {
                    await ingredients.RequireActiveAsync(session, substitute, context.CancellationToken)
                        .ConfigureAwait(false);
                }
                catch (NotFoundError exception)
                {
                    throw AppError.Invalid($"substitute ingredient {substitute} not found", exception);
                }
            }
        }
    }

    private async Task<DrinkRow> RequireActiveRowAsync(OperationContext context, DrinkId id)
    {
        DrinkRow? row = await DrinkRows(context.Session!.Context)
            .SingleOrDefaultAsync(
                row => row.Id == id.Value && row.DeletedAtUtc == null,
                context.CancellationToken)
            .ConfigureAwait(false);
        return row ?? throw AppError.NotFound($"drink {id} not found");
    }

    private ValueTask AuthorizeAsync(OperationContext context, EntityUid action, Drink drink) =>
        authorizer.AuthorizeAsync(context.Principal, action, drink.ToCedarEntity(), context.CancellationToken);

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
            throw AppError.Internal("read drinks", exception);
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

    private static DrinkRow ToRow(Drink drink)
    {
        DrinkRow row = new()
        {
            Id = drink.Id.Value,
            Name = drink.Name,
            Category = drink.Category.Value,
            Glass = drink.Glass.Value,
            Garnish = drink.Recipe.Garnish,
            Description = drink.Description,
            Status = drink.Status.Value,
            DeletedAtUtc = drink.DeletedAt?.UtcDateTime,
        };
        AddRecipeRows(drink, row);
        return row;
    }

    private static void CopyToRow(Drink drink, DrinkRow row)
    {
        row.Name = drink.Name;
        row.Category = drink.Category.Value;
        row.Glass = drink.Glass.Value;
        row.Garnish = drink.Recipe.Garnish;
        row.Description = drink.Description;
        row.Status = drink.Status.Value;
        row.DeletedAtUtc = drink.DeletedAt?.UtcDateTime;
        row.RecipeIngredients.Clear();
        row.RecipeSteps.Clear();
        AddRecipeRows(drink, row);
    }

    private static void AddRecipeRows(Drink drink, DrinkRow row)
    {
        for (int index = 0; index < drink.Recipe.Ingredients.Count; index++)
        {
            RecipeIngredient ingredient = drink.Recipe.Ingredients[index];
            DrinkRecipeIngredientRow ingredientRow = new()
            {
                DrinkId = drink.Id.Value,
                Position = index,
                IngredientId = ingredient.IngredientId.Value,
                Amount = ingredient.Amount.Value,
                Unit = ingredient.Amount.Unit.Value,
                Optional = ingredient.Optional,
            };
            for (int substituteIndex = 0; substituteIndex < ingredient.Substitutes.Count; substituteIndex++)
            {
                ingredientRow.Substitutes.Add(new DrinkRecipeSubstituteRow
                {
                    DrinkId = drink.Id.Value,
                    IngredientPosition = index,
                    Position = substituteIndex,
                    SubstituteId = ingredient.Substitutes[substituteIndex].Value,
                });
            }

            row.RecipeIngredients.Add(ingredientRow);
        }

        for (int index = 0; index < drink.Recipe.Steps.Count; index++)
        {
            row.RecipeSteps.Add(new DrinkRecipeStepRow
            {
                DrinkId = drink.Id.Value,
                Position = index,
                Value = drink.Recipe.Steps[index],
            });
        }
    }

    private static DrinkFilter ToFilter(Drink drink) => new(
        drink.Id.Value,
        drink.Name,
        drink.Category.Value,
        drink.Glass.Value,
        drink.Status.Value,
        drink.Description,
        drink.Tags.Strings().ToArray(),
        new DrinkRecipeFilter(drink.Recipe.Garnish));

    private static void RequireId(DrinkId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("drink id is required");
        }

        _ = DrinkId.Parse(id.Value);
    }

    private static void RequireIngredientId(IngredientId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("ingredient id is required");
        }

        _ = IngredientId.Parse(id.Value);
    }

    private static Operation Command(EntityUid action) => Operation.Command(ActionName(action));
    private static Operation Query(EntityUid action) => Operation.Query(ActionName(action));
    private static string ActionName(EntityUid action) => $"{action.Type}::\"{action.Id}\"";
}
