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
using Mixology.Modules.Ingredients.Authorization;
using Mixology.Modules.Ingredients.Events;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Persistence;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence;

namespace Mixology.Modules.Ingredients;

public sealed class IngredientsModule(
    MixologyStore store,
    ITagReader tags,
    IEntityAuthorizer authorizer,
    TimeProvider timeProvider)
{
    public Task<Ingredient> CreateAsync(
        MixologySession session,
        CreateIngredientRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return session.ExecuteAsync(
            Command(IngredientAuthorization.Create),
            async context =>
            {
                CreateIngredientRequest normalized = request.Normalize();
                Ingredient ingredient = new(
                    IngredientId.New(),
                    normalized.Name,
                    normalized.Category,
                    normalized.Unit,
                    normalized.Description,
                    null,
                    TagCollection.Empty);
                await AuthorizeAsync(context, IngredientAuthorization.Create, ingredient).ConfigureAwait(false);
                await AuthorizeAsync(context, IngredientAuthorization.Create, ingredient).ConfigureAwait(false);

                context.Session!.Context.Add(ToRow(ingredient));
                context.SelectResource(ingredient.EntityUid);
                context.Touch(ingredient.EntityUid);
                context.AddEvent(new IngredientCreated(ingredient));
                return ingredient;
            },
            cancellationToken);
    }

    public Task<Ingredient> GetAsync(
        MixologySession session,
        IngredientId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        return session.ExecuteAsync(
            Query(IngredientAuthorization.Get),
            async context =>
            {
                Ingredient ingredient = await ReadAsync(
                    async database =>
                    {
                        IngredientRow? row = await database.Set<IngredientRow>()
                            .AsNoTracking()
                            .SingleOrDefaultAsync(
                                row => row.Id == id.Value && row.DeletedAtUtc == null,
                                context.CancellationToken)
                            .ConfigureAwait(false);
                        if (row is null)
                        {
                            throw AppError.NotFound($"ingredient {id} not found");
                        }

                        Ingredient loaded = FromRow(row);
                        return loaded with
                        {
                            Tags = await tags.ListAsync(
                                database,
                                loaded.EntityUid,
                                context.CancellationToken).ConfigureAwait(false),
                        };
                    },
                    context.CancellationToken).ConfigureAwait(false);
                await AuthorizeAsync(context, IngredientAuthorization.Get, ingredient).ConfigureAwait(false);
                return ingredient;
            },
            cancellationToken);
    }

    public Task<Ingredient> UpdateAsync(
        MixologySession session,
        UpdateIngredientRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return session.ExecuteAsync(
            Command(IngredientAuthorization.Update),
            async context =>
            {
                UpdateIngredientRequest normalized = request.Normalize();
                IngredientRow row = await RequireActiveRowAsync(context, normalized.Id).ConfigureAwait(false);
                Ingredient current = await WithTagsAsync(
                    context.Session!.Context,
                    FromRow(row),
                    context.CancellationToken).ConfigureAwait(false);
                await AuthorizeAsync(context, IngredientAuthorization.Update, current).ConfigureAwait(false);

                Ingredient updated = (current with
                {
                    Name = normalized.Name ?? current.Name,
                    Category = normalized.Category ?? current.Category,
                    Unit = normalized.Unit ?? current.Unit,
                    Description = normalized.Description ?? current.Description,
                }).Normalize();
                await AuthorizeAsync(context, IngredientAuthorization.Update, updated).ConfigureAwait(false);

                CopyToRow(updated, row);
                context.SelectResource(updated.EntityUid);
                context.Touch(updated.EntityUid);
                context.AddEvent(new IngredientUpdated(updated));
                return updated;
            },
            cancellationToken);
    }

    public Task<Ingredient> RetireAsync(
        MixologySession session,
        RetireIngredientRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return session.ExecuteAsync(
            Command(IngredientAuthorization.Retire),
            async context =>
            {
                RetireIngredientRequest normalized = request.Normalize();
                IngredientRow row = await RequireActiveRowAsync(context, normalized.Id).ConfigureAwait(false);
                Ingredient current = await WithTagsAsync(
                    context.Session!.Context,
                    FromRow(row),
                    context.CancellationToken).ConfigureAwait(false);
                await AuthorizeAsync(context, IngredientAuthorization.Retire, current).ConfigureAwait(false);

                Ingredient? replacement = null;
                if (normalized.Retirement.ReplacementId is { } replacementId)
                {
                    IngredientRow replacementRow;
                    try
                    {
                        replacementRow = await RequireActiveRowAsync(context, replacementId).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (
                        AppError.IsNotFound(exception) && !AppError.IsCancellation(exception))
                    {
                        throw AppError.Invalid(
                            $"replacement ingredient {replacementId} must exist and be active",
                            exception);
                    }

                    replacement = FromRow(replacementRow);
                    await AuthorizeAsync(context, IngredientAuthorization.Get, replacement).ConfigureAwait(false);
                    if (replacement.Category != current.Category)
                    {
                        throw AppError.Invalid(
                            $"replacement category \"{replacement.Category}\" is incompatible with retired category \"{current.Category}\"");
                    }

                    try
                    {
                        _ = Amount.Create(1, current.Unit).Convert(replacement.Unit);
                    }
                    catch (InvalidError exception)
                    {
                        throw AppError.Invalid(
                            $"replacement unit \"{replacement.Unit}\" is incompatible with retired unit \"{current.Unit}\"",
                            exception);
                    }
                }

                DateTimeOffset deletedAt = timeProvider.GetUtcNow().ToUniversalTime();
                Ingredient retired = current with { DeletedAt = deletedAt };
                await AuthorizeAsync(context, IngredientAuthorization.Retire, retired).ConfigureAwait(false);
                row.DeletedAtUtc = deletedAt.UtcDateTime;
                context.SelectResource(retired.EntityUid);
                context.Touch(retired.EntityUid);
                if (replacement is not null)
                {
                    context.Touch(replacement.EntityUid);
                }

                context.AddEvent(new IngredientDeleted(
                    retired,
                    deletedAt,
                    replacement,
                    normalized.Retirement.Ratio));
                return retired;
            },
            cancellationToken);
    }

    public Task<Ingredient> DeleteAsync(
        MixologySession session,
        IngredientId id,
        CancellationToken cancellationToken = default) =>
        RetireAsync(session, new RetireIngredientRequest(id, new Retirement()), cancellationToken);

    public Task<Page<Ingredient>> ListAsync(
        MixologySession session,
        ListIngredientsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ListIngredientsRequest normalized = request.Normalize();
        FilterExpression<IngredientFilter>? expression = Filter.Parse(IngredientFilter.Schema, normalized.Filter);
        return session.ExecuteAsync(
            Query(IngredientAuthorization.List),
            context => ListCoreAsync(context, normalized, expression),
            cancellationToken);
    }

    public async Task<int> CountAsync(
        MixologySession session,
        ListIngredientsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ListIngredientsRequest normalized = request.Normalize() with { Cursor = default, Limit = PageRequest.DefaultLimit };
        return await Paging.CountAsync<Ingredient>(
            async (cursor, token) => await ListAsync(session, normalized with { Cursor = cursor }, token).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlySet<IngredientId>> ActiveIdsAsync(
        MixologySession session,
        IEnumerable<IngredientId> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(ids);
        IngredientId[] requested = ids.Distinct().ToArray();
        foreach (IngredientId id in requested)
        {
            RequireId(id);
        }

        return session.ExecuteAsync(
            Query(IngredientAuthorization.List),
            async context =>
            {
                string[] values = requested.Select(static id => id.Value).ToArray();
                string[] active = await ReadAsync(
                    database => database.Set<IngredientRow>()
                        .AsNoTracking()
                        .Where(row => values.Contains(row.Id) && row.DeletedAtUtc == null)
                        .Select(row => row.Id)
                        .ToArrayAsync(context.CancellationToken),
                    context.CancellationToken).ConfigureAwait(false);
                return (IReadOnlySet<IngredientId>)active.Select(IngredientId.Parse).ToHashSet();
            },
            cancellationToken);
    }

    private async Task<Page<Ingredient>> ListCoreAsync(
        OperationContext context,
        ListIngredientsRequest request,
        FilterExpression<IngredientFilter>? expression)
    {
        (IngredientRow[] Rows, IReadOnlyDictionary<EntityUid, TagCollection> Tags) data = await ReadAsync(
            async database =>
            {
                IQueryable<IngredientRow> query = database.Set<IngredientRow>()
                    .AsNoTracking()
                    .Where(static row => row.DeletedAtUtc == null);
                if (request.Category is { } category)
                {
                    string value = category.Value;
                    query = query.Where(row => row.Category == value);
                }

                Expression<Func<IngredientRow, bool>>? pushdown = expression?.BuildPushdown(IngredientFilter.Persistence);
                if (pushdown is not null)
                {
                    query = query.Where(pushdown);
                }

                IngredientRow[] rows = await query.OrderByDescending(static row => row.Id)
                    .ToArrayAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                IReadOnlyDictionary<EntityUid, TagCollection> loadedTags = await tags.ListTypeAsync(
                    database,
                    EntityIds.IngredientType,
                    rows.Select(static row => row.Id).ToArray(),
                    context.CancellationToken).ConfigureAwait(false);
                return (rows, loadedTags);
            },
            context.CancellationToken).ConfigureAwait(false);

        if (!request.Cursor.IsEmpty)
        {
            data.Rows = data.Rows
                .Where(row => string.CompareOrdinal(row.Id, request.Cursor.Value) < 0)
                .ToArray();
        }

        List<Ingredient> visible = [];
        foreach (IngredientRow row in data.Rows)
        {
            Ingredient ingredient = FromRow(row);
            if (data.Tags.TryGetValue(ingredient.EntityUid, out TagCollection? loadedTags))
            {
                ingredient = ingredient with { Tags = loadedTags };
            }
            IngredientFilter view = ToFilter(ingredient);
            if (expression is not null && !expression.Match(view))
            {
                continue;
            }

            try
            {
                await AuthorizeAsync(context, IngredientAuthorization.List, ingredient).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                AppError.IsPermission(exception) && !AppError.IsCancellation(exception))
            {
                continue;
            }

            visible.Add(ingredient);
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
        return new Page<Ingredient>(visible, next);
    }

    private async Task<IngredientRow> RequireActiveRowAsync(OperationContext context, IngredientId id)
    {
        IngredientRow? row = await context.Session!.Context.Set<IngredientRow>()
            .SingleOrDefaultAsync(
                row => row.Id == id.Value && row.DeletedAtUtc == null,
                context.CancellationToken)
            .ConfigureAwait(false);
        return row ?? throw AppError.NotFound($"ingredient {id} not found");
    }

    private ValueTask AuthorizeAsync(
        OperationContext context,
        EntityUid action,
        Ingredient ingredient) =>
        authorizer.AuthorizeAsync(
            context.Principal,
            action,
            new IngredientAuthorizationResource(
                ingredient.EntityUid,
                ingredient.Tags.ToDictionary(),
                ingredient.Category.Value,
                ingredient.Name,
                ingredient.Unit.Value).ToCedarEntity(),
            context.CancellationToken);

    private async Task<TResult> ReadAsync<TResult>(
        Func<MixologyDbContext, Task<TResult>> query,
        CancellationToken cancellationToken)
    {
        try
        {
            await using StoreSession read = await store.OpenSessionAsync(cancellationToken).ConfigureAwait(false);
            return await query(read.Context).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read ingredients", exception);
        }
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
                TagCollection.Empty).Normalize();
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted ingredient {row.Id}", exception);
        }
    }

    private static IngredientRow ToRow(Ingredient ingredient) => new()
    {
        Id = ingredient.Id.Value,
        Name = ingredient.Name,
        Category = ingredient.Category.Value,
        Unit = ingredient.Unit.Value,
        Description = ingredient.Description,
        DeletedAtUtc = ingredient.DeletedAt?.UtcDateTime,
    };

    private async Task<Ingredient> WithTagsAsync(
        MixologyDbContext database,
        Ingredient ingredient,
        CancellationToken cancellationToken) =>
        ingredient with
        {
            Tags = await tags.ListAsync(database, ingredient.EntityUid, cancellationToken).ConfigureAwait(false),
        };

    private static void CopyToRow(Ingredient ingredient, IngredientRow row)
    {
        row.Name = ingredient.Name;
        row.Category = ingredient.Category.Value;
        row.Unit = ingredient.Unit.Value;
        row.Description = ingredient.Description;
        row.DeletedAtUtc = ingredient.DeletedAt?.UtcDateTime;
    }

    private static IngredientFilter ToFilter(Ingredient ingredient) => new(
        ingredient.Id.Value,
        ingredient.Name,
        ingredient.Category.Value,
        ingredient.Unit.Value,
        ingredient.Description,
        ingredient.Tags.Strings().ToArray());

    private static void RequireId(IngredientId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("id is required");
        }

        _ = IngredientId.Parse(id.Value);
    }

    private static Operation Command(EntityUid action) => Operation.Command(ActionName(action));
    private static Operation Query(EntityUid action) => Operation.Query(ActionName(action));
    private static string ActionName(EntityUid action) => $"{action.Type}::\"{action.Id}\"";
}
