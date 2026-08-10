using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Modules.Inventory.Persistence;
using Mixology.Modules.Orders.Events;

namespace Mixology.Modules.Inventory.Handlers;

public sealed class OrderCancelledHandler : IDomainEventHandler<OrderCancelled>
{
    public async Task HandleAsync(EventHandlerContext context, OrderCancelled domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        InventoryReservationRow[] reservations =
            await InventoryEventPersistence.LoadReservationsForOrderAsync(
                context,
                domainEvent.Order.Id).ConfigureAwait(false);
        if (reservations.Length == 0)
        {
            return;
        }

        string[] ingredientIds = reservations.Select(static row => row.IngredientId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        InventoryRow[] stocks = await InventoryEventPersistence.LoadStocksAsync(
            context,
            ingredientIds).ConfigureAwait(false);
        context.Session.Context.RemoveRange(reservations);
        foreach (InventoryRow stock in stocks)
        {
            context.Touch(InventoryEventPersistence.EntityUid(stock));
        }
    }
}
