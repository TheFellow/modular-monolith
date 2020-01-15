using System.Collections.Generic;

namespace XPL.Framework.Modules.Domain
{
    public abstract class Entity : IEntity
    {
        private List<IDomainEvent> _domainEvents = null!;

        protected void AddDomainEvent(IDomainEvent @event)
        {
            _domainEvents ??= new List<IDomainEvent>();
            _domainEvents.Add(@event);
        }

        void IEntity.ClearEvents() => _domainEvents.Clear();
        IEnumerable<IDomainEvent> IEntity.GetDomainEvents() => _domainEvents.AsReadOnly();
    }
}
