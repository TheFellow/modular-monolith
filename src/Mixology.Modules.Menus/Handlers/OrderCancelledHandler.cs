using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Modules.Menus.Ports;
using Mixology.Modules.Orders.Events;

namespace Mixology.Modules.Menus.Handlers;

public sealed class OrderCancelledHandler(IMenuOperations operations) : IFinalizingDomainEventHandler<OrderCancelled>
{
    public Task FinalizeAsync(EventHandlerContext context, OrderCancelled domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        return OrderAvailabilityReaction.RecalculatePublishedAsync(context, operations);
    }
}
