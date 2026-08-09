using Mixology.Filtering;
using Mixology.Modules.Drinks.Persistence;

namespace Mixology.Modules.Drinks;

internal sealed record DrinkRecipeFilter(string Garnish);

internal sealed record DrinkFilter(
    string Id,
    string Name,
    string Category,
    string Glass,
    string Status,
    string Description,
    string[] Tags,
    DrinkRecipeFilter Recipe)
{
    public static FilterSchema<DrinkFilter> Schema { get; } = new(
    [
        Filter.Field("id", (DrinkFilter item) => item.Id, "Drink ID"),
        Filter.Field("name", (DrinkFilter item) => item.Name, "Drink name"),
        Filter.Field("category", (DrinkFilter item) => item.Category, "Drink category"),
        Filter.Field("glass", (DrinkFilter item) => item.Glass, "Glass type"),
        Filter.Field("status", (DrinkFilter item) => item.Status, "Lifecycle status"),
        Filter.Field("description", (DrinkFilter item) => item.Description, "Drink description"),
        Filter.Field("tags", (DrinkFilter item) => item.Tags, "Tags (key or key=value)"),
        Filter.Field("recipe.garnish", (DrinkFilter item) => item.Recipe.Garnish, "Recipe garnish"),
    ],
    "category == \"cocktail\" && name.contains(\"gin\")",
    "glass in [\"coupe\", \"rocks\"] || recipe.garnish.startsWith(\"lemon\")",
    "status == \"review_required\"",
    "tags contains \"featured\" || tags contains \"region=west\"");

    public static FilterPersistenceMap<DrinkRow> Persistence { get; } = new(
    [
        Filter.PersistedField("id", (DrinkRow row) => row.Id),
        Filter.PersistedField("name", (DrinkRow row) => row.Name),
        Filter.PersistedField("category", (DrinkRow row) => row.Category),
        Filter.PersistedField("glass", (DrinkRow row) => row.Glass),
        Filter.PersistedField("status", (DrinkRow row) => row.Status),
        Filter.PersistedField("description", (DrinkRow row) => row.Description),
        Filter.PersistedField("recipe.garnish", (DrinkRow row) => row.Garnish),
    ]);
}
