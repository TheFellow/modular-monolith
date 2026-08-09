using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Modules.Ingredients.Events;
using Mixology.Modules.Inventory.Persistence;

namespace Mixology.Modules.Inventory.Handlers;

public sealed class IngredientDeletedHandler : IDomainEventHandler<IngredientDeleted>
{
    public async Task HandleAsync(EventHandlerContext context, IngredientDeleted domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        InventoryRow? stock = await InventoryEventPersistence.FindStockAsync(
            context,
            domainEvent.Ingredient.Id).ConfigureAwait(false);
        if (stock is null)
        {
            return;
        }

        context.Session.Context.Remove(stock);
        context.Touch(InventoryEventPersistence.EntityUid(stock));
    }
}
