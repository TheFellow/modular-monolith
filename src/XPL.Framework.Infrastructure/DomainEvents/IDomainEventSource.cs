using System.Collections.Generic;
using XPL.Framework.Domain.Model;

namespace XPL.Framework.Infrastructure.DomainEvents
{
    public interface IDomainEventSource
    {
        IEnumerable<IDomainEvent> GetEvents();
        void ClearEvents();
    }
}
