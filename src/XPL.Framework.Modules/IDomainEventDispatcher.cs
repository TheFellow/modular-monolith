using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules.Domain;

namespace XPL.Framework.Modules
{
    public interface IDomainEventDispatcher
    {
        Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default);
    }
}
