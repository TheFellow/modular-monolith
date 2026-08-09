using Mixology.Application.Operations;

namespace Mixology.Application.Events;

public interface IDomainEventHandler<in TEvent>
{
    Task HandleAsync(EventHandlerContext context, TEvent domainEvent);
}

public interface IPreparingDomainEventHandler<in TEvent> : IDomainEventHandler<TEvent>
{
    Task PrepareAsync(EventHandlerContext context, TEvent domainEvent);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(OperationContext context, object domainEvent);
}

public sealed class DispatchEventsMiddleware(IDomainEventDispatcher dispatcher)
{
    public async Task InvokeAsync(OperationContext context, Operation operation, OperationDelegate next)
    {
        await next(context).ConfigureAwait(false);
        if (operation.Kind != OperationKind.Command)
        {
            return;
        }

        foreach (object domainEvent in context.Events)
        {
            await dispatcher.DispatchAsync(context, domainEvent).ConfigureAwait(false);
        }
    }
}
