namespace Mixology.Presentation.Dashboard;

public interface IDashboardDataSource
{
    Task<int> CountDrinksAsync(CancellationToken cancellationToken);
    Task<int> CountIngredientsAsync(CancellationToken cancellationToken);
    Task<int> CountInventoryAsync(CancellationToken cancellationToken);
    Task<int> CountLowStockAsync(CancellationToken cancellationToken);
    Task<int> CountMenusAsync(CancellationToken cancellationToken);
    Task<int> CountDraftMenusAsync(CancellationToken cancellationToken);
    Task<int> CountPublishedMenusAsync(CancellationToken cancellationToken);
    Task<int> CountOrdersAsync(CancellationToken cancellationToken);
    Task<int> CountPendingOrdersAsync(CancellationToken cancellationToken);
    Task<int> CountAuditAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DashboardActivity>> RecentActivityAsync(
        int limit,
        CancellationToken cancellationToken);
}
