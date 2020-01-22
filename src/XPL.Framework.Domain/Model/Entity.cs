using System.Collections.Generic;
using System.Linq;

namespace XPL.Framework.Domain.Model
{
    public abstract class Entity : IEntity
    {
        private List<IDomainEvent>? _domainEvents;

        protected void AddDomainEvent(IDomainEvent @event)
        {
            _domainEvents ??= new List<IDomainEvent>();
            _domainEvents.Add(@event);
        }

        void IEntity.ClearEvents() => _domainEvents?.Clear();
        IEnumerable<IDomainEvent> IEntity.GetDomainEvents() =>
            _domainEvents?.AsReadOnly() ?? Enumerable.Empty<IDomainEvent>();
    }
}
