using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Infrastructure.DomainEvents;

namespace XPL.Framework.Infrastructure.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        Task<int> CommitAsync(CancellationToken cancellationToken = default);
        event Action? OnSaving, OnSaved;
        void RegisterDomainEventSource(IDomainEventSource domainEventSource);
    }
}
