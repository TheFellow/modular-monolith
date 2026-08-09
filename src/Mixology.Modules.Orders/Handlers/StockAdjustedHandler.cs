using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Modules.Inventory.Events;
using Mixology.Modules.Orders.Persistence;

namespace Mixology.Modules.Orders.Handlers;

public sealed class StockAdjustedHandler : IDomainEventHandler<StockAdjusted>
{
    public async Task HandleAsync(EventHandlerContext context, StockAdjusted domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        IngredientId ingredientId = domainEvent.Inventory.IngredientId;
        OrderRow[] orders = await OrderEventPersistence.LoadActiveByIngredientAsync(
            context,
            ingredientId).ConfigureAwait(false);
        foreach (OrderRow order in orders)
        {
            HashSet<IngredientId> blocked = OrderEventPersistence.BlockedIngredientIds(order).ToHashSet();
            if (domainEvent.Shortage)
            {
                _ = blocked.Add(ingredientId);
            }
            else
            {
                _ = blocked.Remove(ingredientId);
            }

            OrderEventPersistence.ApplyBlockedIngredients(order, blocked);
            context.Touch(OrderEventPersistence.EntityUid(order));
        }
    }
}
