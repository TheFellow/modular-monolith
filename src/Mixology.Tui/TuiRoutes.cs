using Mixology.Presentation.Navigation;

namespace Mixology.Tui;

public sealed record TuiRoute(WorkspaceId Id, string Label, char? Shortcut)
{
    public string Hint => Shortcut is { } shortcut ? $"[{shortcut}] {Label}" : Label;
}

public static class TuiRoutes
{
    public static TuiRoute Dashboard { get; } = new(
        NavigationProjector.DashboardWorkspace,
        "Dashboard",
        null);

    public static TuiRoute Drinks { get; } = new(NavigationProjector.DrinksWorkspace, "Drinks", '1');
    public static TuiRoute Ingredients { get; } = new(NavigationProjector.IngredientsWorkspace, "Ingredients", '2');
    public static TuiRoute Inventory { get; } = new(NavigationProjector.InventoryWorkspace, "Inventory", '3');
    public static TuiRoute Menus { get; } = new(NavigationProjector.MenusWorkspace, "Menus", '4');
    public static TuiRoute Orders { get; } = new(NavigationProjector.OrdersWorkspace, "Orders", '5');
    public static TuiRoute Audit { get; } = new(NavigationProjector.AuditWorkspace, "Audit", '6');
    public static TuiRoute Tags { get; } = new(NavigationProjector.TagsWorkspace, "Tags", '7');

    public static IReadOnlyList<TuiRoute> All { get; } =
    [
        Dashboard,
        Drinks,
        Ingredients,
        Inventory,
        Menus,
        Orders,
        Audit,
        Tags,
    ];
}
