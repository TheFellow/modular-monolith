using System.Collections.Generic;

namespace XPL.Framework.Modules.Domain
{
    public abstract class Entity : IEntity
    {
        private List<IDomainEvent>? _events;
        private List<IDomainEvent> DomainEvents => _events ??= new List<IDomainEvent>();

        protected void AddDomainEvent(IDomainEvent @event) => DomainEvents.Add(@event);

        void IEntity.ClearEvents() => DomainEvents.Clear();
        IEnumerable<IDomainEvent> IEntity.GetDomainEvents() => DomainEvents.ToArray();
    }
}
