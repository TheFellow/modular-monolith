﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Domain.Model;

namespace XPL.Framework.Domain
{
    public interface IDomainEventDispatcher
    {
        Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default);
    }
}
