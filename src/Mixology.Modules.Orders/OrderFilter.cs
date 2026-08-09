using Mixology.Filtering;
using Mixology.Modules.Orders.Persistence;

namespace Mixology.Modules.Orders;

internal sealed record OrderFilter(string Id, string MenuId, string Status, DateTime CreatedAt, string Notes, string[] Tags)
{
    public static FilterSchema<OrderFilter> Schema { get; } = new(
    [
        Filter.Field("id", (OrderFilter item) => item.Id, "Order ID"),
        Filter.Field("menu_id", (OrderFilter item) => item.MenuId, "Menu ID"),
        Filter.Field("status", (OrderFilter item) => item.Status, "Order status"),
        Filter.Field("created_at", (OrderFilter item) => item.CreatedAt, "Creation timestamp"),
        Filter.Field("notes", (OrderFilter item) => item.Notes, "Order notes"),
        Filter.Field("tags", (OrderFilter item) => item.Tags, "Tags (key or key=value)"),
    ],
    "status in [\"pending\", \"completed\"] && !notes.contains(\"test\")",
    "menu_id.startsWith(\"mnu-\") || created_at >= date(\"2026-07-01T00:00:00Z\")",
    "tags contains \"featured\" || tags contains \"region=west\"");
    public static FilterPersistenceMap<OrderRow> Persistence { get; } = new(
    [
        Filter.PersistedField("id", (OrderRow row) => row.Id),
        Filter.PersistedField("menu_id", (OrderRow row) => row.MenuId),
        Filter.PersistedField("status", (OrderRow row) => row.Status),
        Filter.PersistedField("created_at", (OrderRow row) => row.CreatedAtUtc),
        Filter.PersistedField("notes", (OrderRow row) => row.Notes),
    ]);
}
