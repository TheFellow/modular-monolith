using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Modules.Menus.Ports;
using Mixology.Modules.Orders.Events;

namespace Mixology.Modules.Menus.Handlers;

public sealed class OrderCompletedHandler(IMenuOperations operations) : IFinalizingDomainEventHandler<OrderCompleted>
{
    public Task FinalizeAsync(EventHandlerContext context, OrderCompleted domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        return domainEvent.Order.IngredientUsage.Count == 0
            ? Task.CompletedTask
            : OrderAvailabilityReaction.RecalculatePublishedAsync(context, operations);
    }
}
