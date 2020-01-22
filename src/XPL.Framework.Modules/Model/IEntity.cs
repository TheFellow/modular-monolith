using System.Collections.Generic;

namespace XPL.Framework.Domain.Model
{
    public interface IEntity
    {
        void ClearEvents();
        IEnumerable<IDomainEvent> GetDomainEvents();
    }
}
