using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;

namespace Mixology.Modules.Drinks.Models;

public sealed record Recipe
{
    public Recipe(
        IEnumerable<RecipeIngredient> ingredients,
        IEnumerable<string> steps,
        string garnish = "")
    {
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(steps);
        Ingredients = Array.AsReadOnly(ingredients.ToArray());
        Steps = Array.AsReadOnly(steps.ToArray());
        Garnish = garnish ?? string.Empty;
    }

    public IReadOnlyList<RecipeIngredient> Ingredients { get; }
    public IReadOnlyList<string> Steps { get; }
    public string Garnish { get; }

    public Recipe Normalize()
    {
        if (Ingredients.Count == 0)
        {
            throw AppError.Invalid("recipe must have at least 1 ingredient");
        }

        if (Steps.Count == 0)
        {
            throw AppError.Invalid("recipe must have at least 1 step");
        }

        RecipeIngredient[] ingredients = Ingredients.Select(
            static (ingredient, index) => ingredient.Normalize(index)).ToArray();
        string[] steps = Steps.Select(static (step, index) =>
        {
            string normalized = step?.Trim() ?? string.Empty;
            return normalized.Length == 0
                ? throw AppError.Invalid($"recipe step {index}: cannot be blank")
                : normalized;
        }).ToArray();
        return new Recipe(ingredients, steps, Garnish.Trim());
    }

    public void Validate() => _ = Normalize();
}

public sealed record RecipeIngredient
{
    public RecipeIngredient(
        IngredientId ingredientId,
        Amount amount,
        bool optional = false,
        IEnumerable<IngredientId>? substitutes = null)
    {
        IngredientId = ingredientId;
        Amount = amount;
        Optional = optional;
        Substitutes = Array.AsReadOnly(substitutes?.ToArray() ?? []);
    }

    public IngredientId IngredientId { get; }
    public Amount Amount { get; }
    public bool Optional { get; }
    public IReadOnlyList<IngredientId> Substitutes { get; }

    internal RecipeIngredient Normalize(int index)
    {
        if (IngredientId.IsEmpty)
        {
            throw AppError.Invalid($"recipe ingredient {index}: ingredient id is required");
        }

        _ = Kernel.Entities.IngredientId.Parse(IngredientId.Value);
        if (Amount is null)
        {
            throw AppError.Invalid($"recipe ingredient {index}: amount is required");
        }

        Amount.Unit.Validate();
        if (!double.IsFinite(Amount.Value) || Amount.Value <= 0)
        {
            throw AppError.Invalid($"recipe ingredient {index}: amount must be > 0");
        }

        IngredientId[] substitutes = Substitutes.ToArray();
        for (int substituteIndex = 0; substituteIndex < substitutes.Length; substituteIndex++)
        {
            IngredientId substitute = substitutes[substituteIndex];
            if (substitute.IsEmpty)
            {
                throw AppError.Invalid($"recipe ingredient {index} substitute {substituteIndex}: id is required");
            }

            _ = Kernel.Entities.IngredientId.Parse(substitute.Value);
        }

        return new RecipeIngredient(IngredientId, Amount, Optional, substitutes);
    }
}
