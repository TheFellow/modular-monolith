using Mixology.Application.Operations;
using Mixology.Kernel.Errors;

namespace Mixology.Application.Events;

public interface IDomainEventHandler<in TEvent>
{
    Task HandleAsync(EventHandlerContext context, TEvent domainEvent);
}

public interface IPreparingDomainEventHandler<in TEvent> : IDomainEventHandler<TEvent>
{
    Task PrepareAsync(EventHandlerContext context, TEvent domainEvent);
}

/// <summary>
/// Runs after every mutating handler for the event has succeeded and its changes have been
/// flushed inside the shared transaction. Finalizers are for derived projections that must
/// observe the complete post-event state and must not be sensitive to handler ordering.
/// </summary>
public interface IFinalizingDomainEventHandler<in TEvent>
{
    Task FinalizeAsync(EventHandlerContext context, TEvent domainEvent);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(EventHandlerContext context, object domainEvent);
}

public sealed class NullDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(EventHandlerContext context, object domainEvent)
    {
        _ = context;
        _ = domainEvent;
        return Task.CompletedTask;
    }
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

        EventHandlerContext handlerContext = new(context);
        object[] domainEvents = context.Events.ToArray();
        if (domainEvents.Length != 0 && context.Session is not null)
        {
            await handlerContext.FlushAsync("persist command before domain event dispatch").ConfigureAwait(false);
        }

        foreach (object domainEvent in domainEvents)
        {
            try
            {
                await dispatcher.DispatchAsync(handlerContext, domainEvent).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw AppError.Internal($"dispatch event {domainEvent.GetType().FullName}", exception);
            }
        }
    }
}
