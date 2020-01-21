using Functional.Option;
using XPL.Framework.Infrastructure.DomainEvents;
using XPL.Framework.Modules.Domain;

namespace XPL.Framework.Infrastructure.Persistence
{
    public interface IRepository<TModel> : IDomainEventSource
        where TModel : IEntity
    {
        void Add(TModel obj);
        void Delete(TModel obj);
        Option<TModel> TryFind(long id);
    }
}