namespace Mixology.Presentation.Dashboard;

public sealed record DashboardActivity(
    DateTimeOffset Timestamp,
    string Actor,
    string Action);

public sealed record Dashboard(
    int DrinkCount,
    int IngredientCount,
    int InventoryCount,
    int MenuCount,
    int DraftMenus,
    int PublishedMenus,
    int LowStockCount,
    int OrderCount,
    int PendingOrders,
    int AuditCount,
    IReadOnlyList<DashboardActivity> RecentActivity)
{
    public const int UnknownCount = -1;
    public const int RecentActivityLimit = 10;

    public static Dashboard Unknown { get; } = new(
        UnknownCount,
        UnknownCount,
        UnknownCount,
        UnknownCount,
        UnknownCount,
        UnknownCount,
        UnknownCount,
        UnknownCount,
        UnknownCount,
        UnknownCount,
        []);
}

public sealed record DashboardResult(Dashboard Data, Exception? Error = null)
{
    public bool IsPartial => Error is not null;
}
