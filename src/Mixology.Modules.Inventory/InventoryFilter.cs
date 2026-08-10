using Mixology.Filtering;
using Mixology.Modules.Inventory.Persistence;

namespace Mixology.Modules.Inventory;

internal sealed record InventoryFilter(
    string Id,
    string IngredientId,
    double Quantity,
    string Unit,
    DateTime LastUpdated,
    string[] Tags)
{
    public static FilterSchema<InventoryFilter> Schema { get; } = new(
    [
        Filter.Field("id", (InventoryFilter item) => item.Id, "Inventory ID"),
        Filter.Field("ingredient_id", (InventoryFilter item) => item.IngredientId, "Ingredient ID"),
        Filter.Field("quantity", (InventoryFilter item) => item.Quantity, "Quantity on hand"),
        Filter.Field("unit", (InventoryFilter item) => item.Unit, "Measurement unit"),
        Filter.Field("last_updated", (InventoryFilter item) => item.LastUpdated, "Last update timestamp"),
        Filter.Field("tags", (InventoryFilter item) => item.Tags, "Tags (key or key=value)"),
    ],
    "quantity <= 5 && unit == \"ml\"",
    "ingredient_id.startsWith(\"ing-\") || quantity == 0",
    "tags contains \"featured\" || tags contains \"region=west\"");

    public static FilterPersistenceMap<InventoryRow> Persistence { get; } = new(
    [
        Filter.PersistedField("id", (InventoryRow row) => row.Id),
        Filter.PersistedField("ingredient_id", (InventoryRow row) => row.IngredientId),
        Filter.PersistedField("quantity", (InventoryRow row) => row.Quantity),
        Filter.PersistedField("unit", (InventoryRow row) => row.Unit),
        Filter.PersistedField("last_updated", (InventoryRow row) => row.LastUpdatedUtc),
    ]);
}
