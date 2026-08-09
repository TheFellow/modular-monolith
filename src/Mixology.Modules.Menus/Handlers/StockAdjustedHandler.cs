using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Queries;
using Mixology.Modules.Inventory.Events;
using Mixology.Modules.Menus.Persistence;
using Mixology.Modules.Menus.Ports;

namespace Mixology.Modules.Menus.Handlers;

public sealed class StockAdjustedHandler(DrinkQueries drinks, IMenuOperations operations)
    : IDomainEventHandler<StockAdjusted>
{
    public async Task HandleAsync(EventHandlerContext context, StockAdjusted domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        IReadOnlyList<Drink> affected = await drinks.ListByIngredientAsync(
            context.Session,
            domainEvent.Inventory.IngredientId,
            context.CancellationToken).ConfigureAwait(false);
        HashSet<string> drinkIds = affected.Select(static drink => drink.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        MenuRow[] menus = await MenuEventPersistence.LoadByDrinkIdsAsync(
            context,
            drinkIds,
            publishedOnly: true).ConfigureAwait(false);
        foreach (MenuRow menu in menus)
        {
            if (!await MenuAvailabilityReaction.RecalculateAsync(
                context,
                menu,
                operations,
                drinkIds).ConfigureAwait(false))
            {
                continue;
            }

            context.Touch(Mixology.Kernel.Entities.MenuId.Parse(menu.Id).EntityUid);
        }
    }
}
