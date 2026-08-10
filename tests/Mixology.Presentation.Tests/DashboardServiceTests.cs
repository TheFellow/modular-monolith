using Mixology.Kernel.Errors;
using Mixology.Presentation.Dashboard;
using Xunit;
using DashboardData = Mixology.Presentation.Dashboard.Dashboard;

namespace Mixology.Presentation.Tests;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task LoadsIndependentAggregatesAndPreservesFirstNonPermissionError()
    {
        ConflictError first = AppError.Conflict("drink catalog unavailable");
        FakeSource source = new()
        {
            Drinks = () => Task.FromException<int>(first),
            Ingredients = () => Task.FromException<int>(AppError.Permission("denied")),
            Inventory = () => Task.FromResult(3),
            LowStock = () => Task.FromException<int>(AppError.Internal("inventory report unavailable")),
            Menus = () => Task.FromResult(5),
            DraftMenus = () => Task.FromResult(2),
            PublishedMenus = () => Task.FromResult(3),
            Orders = () => Task.FromResult(8),
            PendingOrders = () => Task.FromResult(4),
            Audit = () => Task.FromResult(13),
            Recent = _ => Task.FromResult<IReadOnlyList<DashboardActivity>>(
            [
                new(
                    new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero),
                    "Mixology::Actor::\"manager\"",
                    "Mixology::Order::Action::\"place\""),
            ]),
        };

        DashboardResult result = await DashboardService.LoadAsync(source);

        Assert.Same(first, result.Error);
        Assert.Equal(DashboardData.UnknownCount, result.Data.DrinkCount);
        Assert.Equal(DashboardData.UnknownCount, result.Data.IngredientCount);
        Assert.Equal(DashboardData.UnknownCount, result.Data.LowStockCount);
        Assert.Equal(3, result.Data.InventoryCount);
        Assert.Equal(5, result.Data.MenuCount);
        Assert.Equal(8, result.Data.OrderCount);
        Assert.Equal(13, result.Data.AuditCount);
        Assert.Equal("Mixology::Actor::\"manager\"", Assert.Single(result.Data.RecentActivity).Actor);
        Assert.Equal(FakeSource.ExpectedCalls, source.Calls);
    }

    [Fact]
    public async Task PermissionOnlyFailuresRemainUnknownWithoutFailingDashboard()
    {
        FakeSource source = new()
        {
            Drinks = () => Task.FromException<int>(AppError.Permission("hidden")),
        };

        DashboardResult result = await DashboardService.LoadAsync(source);

        Assert.Null(result.Error);
        Assert.Equal(DashboardData.UnknownCount, result.Data.DrinkCount);
        Assert.Equal(0, result.Data.IngredientCount);
    }

    [Fact]
    public async Task UnknownQueryFailureBecomesSafeInternalWithRetainedCause()
    {
        IOException cause = new("database path must not leak");
        FakeSource source = new()
        {
            Drinks = () => Task.FromException<int>(cause),
        };

        DashboardResult result = await DashboardService.LoadAsync(source);

        InternalError error = Assert.IsType<InternalError>(result.Error);
        Assert.Equal("internal error", error.UserMessage);
        Assert.Same(cause, error.InnerException);
        Assert.Equal(DashboardData.UnknownCount, result.Data.DrinkCount);
    }

    [Fact]
    public async Task CancellationIsNeverDegradedIntoPartialData()
    {
        FakeSource source = new()
        {
            Drinks = () => Task.FromException<int>(new OperationCanceledException()),
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DashboardService.LoadAsync(source));
        Assert.Equal(["drinks"], source.Calls);
    }

    private sealed class FakeSource : IDashboardDataSource
    {
        public static IReadOnlyList<string> ExpectedCalls { get; } =
        [
            "drinks", "ingredients", "inventory", "low-stock", "menus", "draft-menus",
            "published-menus", "orders", "pending-orders", "audit", "recent:10",
        ];

        public Func<Task<int>> Drinks { get; init; } = () => Task.FromResult(0);
        public Func<Task<int>> Ingredients { get; init; } = () => Task.FromResult(0);
        public Func<Task<int>> Inventory { get; init; } = () => Task.FromResult(0);
        public Func<Task<int>> LowStock { get; init; } = () => Task.FromResult(0);
        public Func<Task<int>> Menus { get; init; } = () => Task.FromResult(0);
        public Func<Task<int>> DraftMenus { get; init; } = () => Task.FromResult(0);
        public Func<Task<int>> PublishedMenus { get; init; } = () => Task.FromResult(0);
        public Func<Task<int>> Orders { get; init; } = () => Task.FromResult(0);
        public Func<Task<int>> PendingOrders { get; init; } = () => Task.FromResult(0);
        public Func<Task<int>> Audit { get; init; } = () => Task.FromResult(0);
        public Func<int, Task<IReadOnlyList<DashboardActivity>>> Recent { get; init; } =
            _ => Task.FromResult<IReadOnlyList<DashboardActivity>>([]);
        public List<string> Calls { get; } = [];

        public Task<int> CountDrinksAsync(CancellationToken cancellationToken) => Call("drinks", Drinks);
        public Task<int> CountIngredientsAsync(CancellationToken cancellationToken) => Call("ingredients", Ingredients);
        public Task<int> CountInventoryAsync(CancellationToken cancellationToken) => Call("inventory", Inventory);
        public Task<int> CountLowStockAsync(CancellationToken cancellationToken) => Call("low-stock", LowStock);
        public Task<int> CountMenusAsync(CancellationToken cancellationToken) => Call("menus", Menus);
        public Task<int> CountDraftMenusAsync(CancellationToken cancellationToken) => Call("draft-menus", DraftMenus);
        public Task<int> CountPublishedMenusAsync(CancellationToken cancellationToken) =>
            Call("published-menus", PublishedMenus);
        public Task<int> CountOrdersAsync(CancellationToken cancellationToken) => Call("orders", Orders);
        public Task<int> CountPendingOrdersAsync(CancellationToken cancellationToken) =>
            Call("pending-orders", PendingOrders);
        public Task<int> CountAuditAsync(CancellationToken cancellationToken) => Call("audit", Audit);

        public Task<IReadOnlyList<DashboardActivity>> RecentActivityAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            Calls.Add($"recent:{limit}");
            return Recent(limit);
        }

        private Task<int> Call(string name, Func<Task<int>> load)
        {
            Calls.Add(name);
            return load();
        }
    }
}
