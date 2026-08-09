using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Modules.Ingredients.Events;
using Mixology.Modules.Orders.Persistence;

namespace Mixology.Modules.Orders.Handlers;

public sealed class IngredientDeletedHandler : IDomainEventHandler<IngredientDeleted>
{
    public async Task HandleAsync(EventHandlerContext context, IngredientDeleted domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        IngredientId ingredientId = domainEvent.Ingredient.Id;
        OrderRow[] orders = await OrderEventPersistence.LoadActiveByIngredientAsync(
            context,
            ingredientId).ConfigureAwait(false);
        foreach (OrderRow order in orders)
        {
            OrderEventPersistence.ApplyBlockedIngredients(
                order,
                OrderEventPersistence.BlockedIngredientIds(order).Append(ingredientId));
            context.Touch(OrderEventPersistence.EntityUid(order));
        }
    }
}
