using MediatR;

namespace XPL.Framework.Modules.Domain
{
    public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
        where TEvent : IDomainEvent
    {

    }
}
