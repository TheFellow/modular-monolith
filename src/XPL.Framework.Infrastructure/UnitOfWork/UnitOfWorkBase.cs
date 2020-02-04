using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Ports;
using XPL.Framework.Infrastructure.DomainEvents;

namespace XPL.Framework.Infrastructure.UnitOfWork
{
    public abstract class UnitOfWorkBase<TContext> : IUnitOfWork
        where TContext : DbContext
    {
        public TContext DbContext { get; }
        private readonly IDomainEventDispatcher _domainEventDispatcher;
        private List<IDomainEventSource>? _domainEventSources;

        public UnitOfWorkBase(Func<TContext> dbContextFactory, IDomainEventDispatcher domainEventDispatcher)
        {
            DbContext = dbContextFactory();
            _domainEventDispatcher = domainEventDispatcher;
        }

        public void RegisterDomainEventSource(IDomainEventSource domainEventSource)
        {
            _domainEventSources ??= new List<IDomainEventSource>();
            _domainEventSources.Add(domainEventSource);
        }

        public async Task<int> CommitAsync(Guid correlationId, CancellationToken cancellationToken = default)
        {
            await DispatchEvents(correlationId, cancellationToken);
            return await SaveChangesAsync(cancellationToken);
        }

        private async Task DispatchEvents(Guid correlationId, CancellationToken cancellationToken)
        {
            List<IDomainEventSource> entities;

            while (true)
            {
                entities = (_domainEventSources ?? Enumerable.Empty<IDomainEventSource>())
                    .Where(src => src.GetEvents().Any())
                    .ToList();

                if (entities.Count == 0)
                    break;

                var events = entities.SelectMany(e => e.GetEvents()).ToList();
                entities.ForEach(e => e.ClearEvents());
                events.ForEach(e => e.CorrelationId = correlationId);

                await _domainEventDispatcher.DispatchEventsAsync(events, cancellationToken);
            }
        }

        public event Action? OnSaving, OnSaved;

        private async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            OnSaving?.Invoke();
            int returnValue = await DbContext.SaveChangesAsync(cancellationToken);
            OnSaved?.Invoke();
            return returnValue;
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _domainEventSources?.Clear();
                    DbContext.Dispose();
                }

                _disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
