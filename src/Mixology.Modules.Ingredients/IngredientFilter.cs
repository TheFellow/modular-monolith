using Mixology.Filtering;
using Mixology.Modules.Ingredients.Persistence;

namespace Mixology.Modules.Ingredients;

internal sealed record IngredientFilter(
    string Id,
    string Name,
    string Category,
    string Unit,
    string Description,
    string[] Tags)
{
    public static FilterSchema<IngredientFilter> Schema { get; } = new(
    [
        Filter.Field("id", (IngredientFilter item) => item.Id, "Ingredient ID"),
        Filter.Field("name", (IngredientFilter item) => item.Name, "Ingredient name"),
        Filter.Field("category", (IngredientFilter item) => item.Category, "Ingredient category"),
        Filter.Field("unit", (IngredientFilter item) => item.Unit, "Measurement unit"),
        Filter.Field("description", (IngredientFilter item) => item.Description, "Ingredient description"),
        Filter.Field("tags", (IngredientFilter item) => item.Tags, "Tags (key or key=value)"),
    ],
    "category == \"spirit\" && name.contains(\"gin\")",
    "unit in [\"ml\", \"oz\"] && !description.contains(\"seasonal\")",
    "tags contains \"featured\" || tags contains \"region=west\"");

    public static FilterPersistenceMap<IngredientRow> Persistence { get; } = new(
    [
        Filter.PersistedField("id", (IngredientRow row) => row.Id),
        Filter.PersistedField("name", (IngredientRow row) => row.Name),
        Filter.PersistedField("category", (IngredientRow row) => row.Category),
        Filter.PersistedField("unit", (IngredientRow row) => row.Unit),
        Filter.PersistedField("description", (IngredientRow row) => row.Description),
    ]);
}
