using System.Collections.Generic;

namespace XPL.Framework.Modules.Domain
{
    public interface IEntity
    {
        void ClearEvents();
        IEnumerable<IDomainEvent> GetDomainEvents();
    }
}
