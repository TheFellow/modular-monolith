using Mixology.Application;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Audit;
using Mixology.Modules.Audit.Models;
using Mixology.Modules.Audit.Requests;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Requests;

namespace Mixology.Presentation.Dashboard;

public sealed class ModuleDashboardDataSourceFactory(
    DrinksModule drinks,
    IngredientsModule ingredients,
    InventoryModule inventory,
    MenusModule menus,
    OrdersModule orders,
    AuditModule audit)
{
    public IDashboardDataSource Bind(MixologySession session) => new SessionSource(
        session,
        drinks,
        ingredients,
        inventory,
        menus,
        orders,
        audit);

    private sealed class SessionSource(
        MixologySession session,
        DrinksModule drinks,
        IngredientsModule ingredients,
        InventoryModule inventory,
        MenusModule menus,
        OrdersModule orders,
        AuditModule audit) : IDashboardDataSource
    {
        public Task<int> CountDrinksAsync(CancellationToken cancellationToken) =>
            drinks.CountAsync(session, new ListDrinksRequest(), cancellationToken);

        public Task<int> CountIngredientsAsync(CancellationToken cancellationToken) =>
            ingredients.CountAsync(session, new ListIngredientsRequest(), cancellationToken);

        public Task<int> CountInventoryAsync(CancellationToken cancellationToken) =>
            inventory.CountAsync(session, new ListInventoryRequest(), cancellationToken);

        public Task<int> CountLowStockAsync(CancellationToken cancellationToken) =>
            inventory.CountAsync(
                session,
                new ListInventoryRequest(LowStock: ListInventoryRequest.DefaultLowStockThreshold),
                cancellationToken);

        public Task<int> CountMenusAsync(CancellationToken cancellationToken) =>
            menus.CountAsync(session, new ListMenusRequest(), cancellationToken);

        public Task<int> CountDraftMenusAsync(CancellationToken cancellationToken) =>
            menus.CountAsync(session, new ListMenusRequest(Status: MenuStatus.Draft), cancellationToken);

        public Task<int> CountPublishedMenusAsync(CancellationToken cancellationToken) =>
            menus.CountAsync(session, new ListMenusRequest(Status: MenuStatus.Published), cancellationToken);

        public Task<int> CountOrdersAsync(CancellationToken cancellationToken) =>
            orders.CountAsync(session, new ListOrdersRequest(), cancellationToken);

        public Task<int> CountPendingOrdersAsync(CancellationToken cancellationToken) =>
            orders.CountAsync(session, new ListOrdersRequest(Status: OrderStatus.Pending), cancellationToken);

        public Task<int> CountAuditAsync(CancellationToken cancellationToken) =>
            audit.CountAsync(session, new ListAuditEntriesRequest(), cancellationToken);

        public async Task<IReadOnlyList<DashboardActivity>> RecentActivityAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            Mixology.Kernel.Paging.Page<AuditEntry> page = await audit.ListAsync(
                session,
                new ListAuditEntriesRequest(Limit: limit),
                cancellationToken).ConfigureAwait(false);
            return page.Items.Select(static entry => new DashboardActivity(
                entry.CompletedAt == default ? entry.StartedAt : entry.CompletedAt,
                entry.Principal.ToCedarUid().MarshalCedar(),
                entry.Action)).ToArray();
        }
    }
}
