using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Errors;
using Mixology.Modules.Menus.Events;
using Mixology.Modules.Menus.Persistence;
using Mixology.Modules.Menus.Ports;

namespace Mixology.Modules.Menus.Handlers;

public sealed class MenuPublishedHandler(IMenuOperations operations) : IDomainEventHandler<MenuPublished>
{
    public async Task HandleAsync(EventHandlerContext context, MenuPublished domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        MenuRow[] rows = await MenuEventPersistence.LoadByIdsAsync(
            context,
            [domainEvent.Menu.Id.Value]).ConfigureAwait(false);
        MenuRow row = rows.SingleOrDefault()
            ?? throw AppError.Internal($"recalculate published menu {domainEvent.Menu.Id}: row is missing");
        if (await MenuAvailabilityReaction.RecalculateAsync(
            context,
            row,
            operations).ConfigureAwait(false))
        {
            context.Touch(domainEvent.Menu.EntityUid);
        }
    }
}
