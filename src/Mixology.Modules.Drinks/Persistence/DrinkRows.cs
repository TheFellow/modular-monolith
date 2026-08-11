using Mixology.Persistence;

namespace Mixology.Modules.Drinks.Persistence;

internal sealed class DrinkRow : IRevisionedRow
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public required string Glass { get; set; }
    public required string Garnish { get; set; }
    public required string Description { get; set; }
    public required string Status { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long Revision { get; set; }
    public List<DrinkRecipeIngredientRow> RecipeIngredients { get; } = [];
    public List<DrinkRecipeStepRow> RecipeSteps { get; } = [];
}

internal sealed class DrinkRecipeIngredientRow
{
    public required string DrinkId { get; init; }
    public required int Position { get; init; }
    public required string IngredientId { get; set; }
    public required double Amount { get; set; }
    public required string Unit { get; set; }
    public required bool Optional { get; set; }
    public List<DrinkRecipeSubstituteRow> Substitutes { get; } = [];
}

internal sealed class DrinkRecipeSubstituteRow
{
    public required string DrinkId { get; init; }
    public required int IngredientPosition { get; init; }
    public required int Position { get; init; }
    public required string SubstituteId { get; set; }
}

internal sealed class DrinkRecipeStepRow
{
    public required string DrinkId { get; init; }
    public required int Position { get; init; }
    public required string Value { get; set; }
}
