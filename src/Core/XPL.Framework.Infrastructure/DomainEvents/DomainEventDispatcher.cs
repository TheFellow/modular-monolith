using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules;
using XPL.Framework.Modules.Domain;

namespace XPL.Framework.Infrastructure.DomainEvents
{
    public class DomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly IMediator _mediator;

        public DomainEventDispatcher(IMediator mediator) => _mediator = mediator;

        public async Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            foreach (var @event in events)
                await _mediator.Publish(@event, cancellationToken);
        }
    }
}
