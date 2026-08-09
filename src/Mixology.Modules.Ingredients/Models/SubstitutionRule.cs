using System.Diagnostics.CodeAnalysis;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Quality;

namespace Mixology.Modules.Ingredients.Models;

public sealed record SubstitutionRule(
    IngredientId IngredientId,
    IngredientId SubstituteId,
    double Ratio,
    Quality QualityImpact,
    string Notes)
{
    public void Validate()
    {
        if (IngredientId.IsEmpty)
        {
            throw AppError.Invalid("ingredient id is required");
        }

        _ = global::Mixology.Kernel.Entities.IngredientId.Parse(IngredientId.Value);
        if (SubstituteId.IsEmpty)
        {
            throw AppError.Invalid("substitute id is required");
        }

        _ = global::Mixology.Kernel.Entities.IngredientId.Parse(SubstituteId.Value);
        if (!double.IsFinite(Ratio) || Ratio <= 0)
        {
            throw AppError.Invalid("ratio must be a finite number greater than zero");
        }

        QualityImpact.Validate();
    }
}

public static class IngredientSubstitutionCatalog
{
    private static readonly CatalogRule[] Rules =
    [
        new("lime-juice", "lemon-juice", 1, Quality.Similar, "Citrus swap; expect a slightly different profile"),
        new("lemon-juice", "lime-juice", 1, Quality.Similar, "Citrus swap; expect a slightly different profile"),
        new("simple-syrup", "honey-syrup", 0.75, Quality.Different, "Honey is sweeter; reduce amount"),
        new("bourbon", "rye-whiskey", 1, Quality.Equivalent, "Comparable spirit substitution"),
        new("fresh-mint", "dried-mint", 0.5, Quality.Different, "Dried herbs are more concentrated"),
    ];

    public static IReadOnlyList<SubstitutionRule> Resolve(
        IngredientId ingredientId,
        IEnumerable<Ingredient> activeIngredients)
    {
        ArgumentNullException.ThrowIfNull(activeIngredients);
        Ingredient[] ingredients = activeIngredients.Where(value => !value.IsRetired).ToArray();
        Ingredient original = ingredients.FirstOrDefault(value => value.Id == ingredientId)
            ?? throw AppError.NotFound($"ingredient {ingredientId} not found");
        Dictionary<string, IngredientId> idsByKey = new(StringComparer.Ordinal);
        foreach (Ingredient ingredient in ingredients)
        {
            idsByKey[ToKey(ingredient.Name)] = ingredient.Id;
        }
        string originalKey = ToKey(original.Name);

        return Rules
            .Where(rule => rule.IngredientKey == originalKey && idsByKey.ContainsKey(rule.SubstituteKey))
            .Select(rule => new SubstitutionRule(
                original.Id,
                idsByKey[rule.SubstituteKey],
                rule.Ratio,
                rule.QualityImpact,
                rule.Notes))
            .ToArray();
    }

    public static bool TryLookup(
        IngredientId original,
        IngredientId substitute,
        IEnumerable<Ingredient> activeIngredients,
        [NotNullWhen(true)] out SubstitutionRule? rule)
    {
        rule = Resolve(original, activeIngredients).FirstOrDefault(value => value.SubstituteId == substitute);
        return rule is not null;
    }

    public static string ToKey(string name) =>
        string.Join('-', name.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private sealed record CatalogRule(
        string IngredientKey,
        string SubstituteKey,
        double Ratio,
        Quality QualityImpact,
        string Notes);
}
