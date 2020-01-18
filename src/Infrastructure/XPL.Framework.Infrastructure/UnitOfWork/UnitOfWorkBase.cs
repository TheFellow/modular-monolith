﻿using Functional.Either;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Ports.Bus;
using XPL.Framework.Infrastructure.DomainEvents;
using XPL.Framework.Modules;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Infrastructure.UnitOfWork
{
    public abstract class UnitOfWorkBase<TContext> : IUnitOfWork, IBus
        where TContext : DbContext
    {
        protected TContext DbContext { get; }
        private readonly IDomainEventDispatcher _domainEventDispatcher;
        private readonly IBus _bus;
        private List<IDomainEventSource>? _domainEventSources;

        public UnitOfWorkBase(Func<TContext> dbContextFactory, IBus bus, IDomainEventDispatcher domainEventDispatcher)
        {
            DbContext = dbContextFactory();
            _bus = bus;
            _domainEventDispatcher = domainEventDispatcher;
        }

        public void RegisterDomainEventSource(IDomainEventSource domainEventSource)
        {
            _domainEventSources ??= new List<IDomainEventSource>();
            _domainEventSources.Add(domainEventSource);
        }

        public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            var entities = (_domainEventSources ?? Enumerable.Empty<IDomainEventSource>())
                .Where(src => src.GetEvents().Any())
                .ToList();

            var events = entities.SelectMany(e => e.GetEvents());
            entities.ForEach(e => e.ClearEvents());

            await _domainEventDispatcher.DispatchEventsAsync(events, cancellationToken);

            return await SaveChangesAsync(cancellationToken);
        }

        public event Action? OnSaving, OnSaved;

        private async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            OnSaving?.Invoke();
            int returnValue = await DbContext.SaveChangesAsync(cancellationToken);
            OnSaved?.Invoke();
            return returnValue;
        }

        public Task<Either<CommandError, TResult>> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) => _bus.ExecuteCommandAsync(command, cancellationToken);
        public Task ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) => _bus.ExecuteQueryAsync(query, cancellationToken);

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
