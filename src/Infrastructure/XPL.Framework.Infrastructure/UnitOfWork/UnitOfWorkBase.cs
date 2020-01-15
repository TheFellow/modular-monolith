using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules;
using XPL.Framework.Modules.Domain;

namespace XPL.Framework.Infrastructure.UnitOfWork
{
    public abstract class UnitOfWorkBase : IUnitOfWork
    {
        private readonly DbContext _dbContext;
        private readonly IDomainEventDispatcher _domainEventDispatcher;

        public UnitOfWorkBase(DbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
        {
            _dbContext = dbContext;
            _domainEventDispatcher = domainEventDispatcher;
        }

        public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            var entities = _dbContext.ChangeTracker
                .Entries()
                .OfType<IEntity>()
                .Where(e => e.GetDomainEvents().Any())
                .ToList();

            var events = entities.SelectMany(e => e.GetDomainEvents());
            entities.ForEach(e => e.ClearEvents());

            await _domainEventDispatcher.DispatchEventsAsync(events, cancellationToken);

            return await SaveChangesAsync(cancellationToken);
        }

        private Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);
    }
}
