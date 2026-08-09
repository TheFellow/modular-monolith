using Mixology.Filtering;
using Mixology.Modules.Menus.Persistence;

namespace Mixology.Modules.Menus;

internal sealed record MenuFilter(
    string Id,
    string Name,
    string Description,
    string Status,
    DateTime CreatedAt,
    string[] Tags)
{
    public static FilterSchema<MenuFilter> Schema { get; } = new(
    [
        Filter.Field("id", (MenuFilter menu) => menu.Id, "Menu ID"),
        Filter.Field("name", (MenuFilter menu) => menu.Name, "Menu name"),
        Filter.Field("description", (MenuFilter menu) => menu.Description, "Menu description"),
        Filter.Field("status", (MenuFilter menu) => menu.Status, "Menu lifecycle status"),
        Filter.Field("created_at", (MenuFilter menu) => menu.CreatedAt, "Creation timestamp"),
        Filter.Field("tags", (MenuFilter menu) => menu.Tags, "Tags (key or key=value)"),
    ],
    "status == \"published\" && name.contains(\"Summer\")",
    "created_at >= date(\"2026-01-01T00:00:00Z\")",
    "tags contains \"featured\"");

    public static FilterPersistenceMap<MenuRow> Persistence { get; } = new(
    [
        Filter.PersistedField("id", (MenuRow row) => row.Id),
        Filter.PersistedField("name", (MenuRow row) => row.Name),
        Filter.PersistedField("description", (MenuRow row) => row.Description),
        Filter.PersistedField("status", (MenuRow row) => row.Status),
        Filter.PersistedField("created_at", (MenuRow row) => row.CreatedAtUtc),
    ]);
}
