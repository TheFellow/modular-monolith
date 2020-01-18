using System.Collections.Generic;
using XPL.Framework.Modules.Domain;

namespace XPL.Framework.Infrastructure.DomainEvents
{
    public interface IDomainEventSource
    {
        IEnumerable<IDomainEvent> GetEvents();
        void ClearEvents();
    }
}
