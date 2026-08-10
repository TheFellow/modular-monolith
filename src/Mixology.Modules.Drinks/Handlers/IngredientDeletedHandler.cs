using Microsoft.EntityFrameworkCore;
using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Persistence;
using Mixology.Modules.Drinks.Queries;
using Mixology.Modules.Ingredients.Events;

namespace Mixology.Modules.Drinks.Handlers;

public sealed class IngredientDeletedHandler(DrinkQueries drinks)
    : IPreparingDomainEventHandler<IngredientDeleted>
{
    private RewritePlan[] plans = [];

    public async Task PrepareAsync(EventHandlerContext context, IngredientDeleted domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        plans = [];
        IReadOnlyList<Drink> affected = await drinks.ListByIngredientAsync(
            context.Session,
            domainEvent.Ingredient.Id,
            context.CancellationToken).ConfigureAwait(false);
        plans = affected.Select(drink => Rewrite(drink, domainEvent)).ToArray();
    }

    public async Task HandleAsync(EventHandlerContext context, IngredientDeleted domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (plans.Length == 0)
        {
            return;
        }

        Dictionary<string, DrinkRow> rows = await LoadRowsAsync(context, plans).ConfigureAwait(false);
        foreach (RewritePlan plan in plans)
        {
            if (!rows.TryGetValue(plan.Drink.Id.Value, out DrinkRow? row))
            {
                throw AppError.Internal($"rewrite drink {plan.Drink.Id}: prepared row is missing");
            }

            Apply(row, plan.Drink);
            context.Touch(plan.Drink.EntityUid);
        }
    }

    private static RewritePlan Rewrite(Drink drink, IngredientDeleted domainEvent)
    {
        IngredientId retired = domainEvent.Ingredient.Id;
        IngredientId? replacement = domainEvent.Replacement?.Id;
        List<RecipeIngredient> rewritten = new(drink.Recipe.Ingredients.Count);
        bool requiresReview = false;
        foreach (RecipeIngredient current in drink.Recipe.Ingredients)
        {
            IngredientId[] substitutes = current.Substitutes
                .Select(id => id == retired && replacement is { } replacementId ? replacementId : id)
                .ToArray();
            substitutes = CompactSubstitutes(substitutes, current.IngredientId, retired);
            RecipeIngredient ingredient = new(
                current.IngredientId,
                current.Amount,
                current.Optional,
                substitutes);
            if (current.IngredientId != retired)
            {
                rewritten.Add(ingredient);
                continue;
            }

            if (domainEvent.Replacement is { } replacementIngredient)
            {
                try
                {
                    ingredient = new RecipeIngredient(
                        replacementIngredient.Id,
                        current.Amount.Convert(replacementIngredient.Unit).Multiply(domainEvent.ReplacementRatio),
                        current.Optional,
                        substitutes.Where(id => id != replacementIngredient.Id));
                }
                catch (InvalidError exception)
                {
                    throw AppError.Internal($"rewrite drink {drink.Id} replacement amount", exception);
                }

                rewritten.Add(ingredient);
                continue;
            }

            if (current.Optional)
            {
                continue;
            }

            requiresReview = true;
            rewritten.Add(ingredient);
        }

        Drink review = drink with
        {
            Recipe = new Recipe(rewritten, drink.Recipe.Steps, drink.Recipe.Garnish),
            Status = requiresReview ? DrinkStatus.ReviewRequired : drink.Status,
        };
        return new RewritePlan(review);
    }

    private static IngredientId[] CompactSubstitutes(
        IEnumerable<IngredientId> ids,
        IngredientId primary,
        IngredientId retired)
    {
        HashSet<IngredientId> seen = [];
        List<IngredientId> compact = [];
        foreach (IngredientId id in ids)
        {
            if (id == retired || id == primary || !seen.Add(id))
            {
                continue;
            }

            compact.Add(id);
        }

        return compact.ToArray();
    }

    private static async Task<Dictionary<string, DrinkRow>> LoadRowsAsync(
        EventHandlerContext context,
        IReadOnlyList<RewritePlan> plans)
    {
        try
        {
            string[] ids = plans.Select(static plan => plan.Drink.Id.Value).ToArray();
            return await context.Session.Context.Set<DrinkRow>()
                .Include(row => row.RecipeIngredients)
                .ThenInclude(row => row.Substitutes)
                .Where(row => ids.Contains(row.Id) && row.DeletedAtUtc == null)
                .ToDictionaryAsync(static row => row.Id, StringComparer.Ordinal, context.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("load prepared drinks for ingredient retirement", exception);
        }
    }

    private static void Apply(DrinkRow row, Drink rewritten)
    {
        row.Status = rewritten.Status.Value;
        row.RecipeIngredients.Clear();
        for (int index = 0; index < rewritten.Recipe.Ingredients.Count; index++)
        {
            RecipeIngredient ingredient = rewritten.Recipe.Ingredients[index];
            DrinkRecipeIngredientRow ingredientRow = new()
            {
                DrinkId = rewritten.Id.Value,
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
                    DrinkId = rewritten.Id.Value,
                    IngredientPosition = index,
                    Position = substituteIndex,
                    SubstituteId = ingredient.Substitutes[substituteIndex].Value,
                });
            }

            row.RecipeIngredients.Add(ingredientRow);
        }
    }

    private sealed record RewritePlan(Drink Drink);
}
