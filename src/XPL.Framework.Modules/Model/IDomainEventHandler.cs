using MediatR;

namespace XPL.Framework.Domain.Model
{
    public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
        where TEvent : IDomainEvent
    {

    }
}
