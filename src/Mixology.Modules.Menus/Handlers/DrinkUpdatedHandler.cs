using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Modules.Drinks.Events;
using Mixology.Modules.Menus.Persistence;
using Mixology.Modules.Menus.Ports;

namespace Mixology.Modules.Menus.Handlers;

public sealed class DrinkUpdatedHandler(IMenuOperations operations) : IDomainEventHandler<DrinkUpdated>
{
    public async Task HandleAsync(EventHandlerContext context, DrinkUpdated domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        string changedId = domainEvent.Drink.Id.Value;
        MenuRow[] menus = await MenuEventPersistence.LoadByDrinkIdsAsync(
            context,
            [changedId],
            publishedOnly: true).ConfigureAwait(false);
        IReadOnlySet<string> selected = new HashSet<string>([changedId], StringComparer.Ordinal);
        foreach (MenuRow menu in menus)
        {
            if (!await MenuAvailabilityReaction.RecalculateAsync(
                context,
                menu,
                operations,
                selected).ConfigureAwait(false))
            {
                continue;
            }

            context.Touch(Mixology.Kernel.Entities.MenuId.Parse(menu.Id).EntityUid);
        }
    }
}
