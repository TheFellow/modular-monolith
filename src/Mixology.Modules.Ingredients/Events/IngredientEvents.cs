using Mixology.Modules.Ingredients.Models;

namespace Mixology.Modules.Ingredients.Events;

public sealed record IngredientCreated(Ingredient Ingredient);

public sealed record IngredientUpdated(Ingredient Ingredient);

public sealed record IngredientDeleted(
    Ingredient Ingredient,
    DateTimeOffset DeletedAt,
    Ingredient? Replacement,
    double ReplacementRatio);
