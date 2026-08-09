using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Modules.Drinks.Events;
using Mixology.Modules.Menus.Persistence;

namespace Mixology.Modules.Menus.Handlers;

public sealed class DrinkDeletedHandler : IDomainEventHandler<DrinkDeleted>
{
    public async Task HandleAsync(EventHandlerContext context, DrinkDeleted domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        string deletedId = domainEvent.Drink.Id.Value;
        MenuRow[] menus = await MenuEventPersistence.LoadByDrinkIdsAsync(
            context,
            [deletedId]).ConfigureAwait(false);
        foreach (MenuRow menu in menus)
        {
            int removed = menu.Items.RemoveAll(item =>
                string.Equals(item.DrinkId, deletedId, StringComparison.Ordinal));
            if (removed == 0)
            {
                continue;
            }

            context.Touch(Mixology.Kernel.Entities.MenuId.Parse(menu.Id).EntityUid);
        }
    }
}
