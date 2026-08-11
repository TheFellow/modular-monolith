using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Presentation;
using Mixology.Modules.Orders.Requests;
using Mixology.Presentation.Mutations;

namespace Mixology.Gui.Workspaces.Orders;

public sealed record OrderCatalog(IReadOnlyList<Menu> Menus, IReadOnlyDictionary<DrinkId, Drink> Drinks);

public interface IOrderDesktopOperations
{
    Task<Page<Order>> ListAsync(ListOrdersRequest request, CancellationToken cancellationToken);
    Task<Order> GetAsync(OrderId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectAsync(Order? selected, CancellationToken cancellationToken);
    Task<OrderCatalog> CatalogAsync(CancellationToken cancellationToken);
    Task<Order> PlaceAsync(PlaceOrderRequest request, TagCollection tags, CancellationToken cancellationToken);
    Task<Order> CompleteAsync(OrderId id, CancellationToken cancellationToken);
    Task<Order> CancelAsync(OrderId id, CancellationToken cancellationToken);
}

internal sealed class ModuleOrderDesktopOperations(
    OrdersModule orders,
    MenusModule menus,
    DrinksModule drinks,
    OrderActionProjector projector,
    TaggedMutationCoordinator taggedMutations,
    MixologySession session,
    Actor actor) : IOrderDesktopOperations
{
    public Task<Page<Order>> ListAsync(ListOrdersRequest request, CancellationToken cancellationToken) =>
        orders.ListAsync(session, request, cancellationToken);

    public Task<Order> GetAsync(OrderId id, CancellationToken cancellationToken) =>
        orders.GetAsync(session, id, cancellationToken);

    public Task<IReadOnlyList<ActionState>> ProjectAsync(Order? selected, CancellationToken cancellationToken) =>
        projector.ProjectAsync(actor, selected, cancellationToken);

    public async Task<OrderCatalog> CatalogAsync(CancellationToken cancellationToken)
    {
        List<Menu> published = [];
        Cursor menuCursor = default;
        do
        {
            Page<Menu> page = await menus.ListAsync(
                session,
                new ListMenusRequest(Cursor: menuCursor),
                cancellationToken).ConfigureAwait(false);
            published.AddRange(page.Items);
            menuCursor = page.Next;
        }
        while (!menuCursor.IsEmpty);

        Dictionary<DrinkId, Drink> catalog = [];
        Cursor drinkCursor = default;
        do
        {
            Page<Drink> page = await drinks.ListAsync(
                session,
                new ListDrinksRequest(Cursor: drinkCursor),
                cancellationToken).ConfigureAwait(false);
            foreach (Drink drink in page.Items)
            {
                catalog[drink.Id] = drink;
            }

            drinkCursor = page.Next;
        }
        while (!drinkCursor.IsEmpty);

        return new OrderCatalog(published, catalog);
    }

    public Task<Order> PlaceAsync(
        PlaceOrderRequest request,
        TagCollection tags,
        CancellationToken cancellationToken) => taggedMutations.RunAsync(
            session,
            (active, token) => orders.PlaceAsync(active, request, token),
            tags,
            static value => value.EntityUid,
            static (value, applied) => value with { Tags = applied },
            cancellationToken);

    public Task<Order> CompleteAsync(OrderId id, CancellationToken cancellationToken) =>
        orders.CompleteAsync(session, id, cancellationToken);

    public Task<Order> CancelAsync(OrderId id, CancellationToken cancellationToken) =>
        orders.CancelAsync(session, id, cancellationToken);
}
