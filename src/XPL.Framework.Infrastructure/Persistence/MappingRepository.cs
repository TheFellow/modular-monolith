using Functional.Option;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using XPL.Framework.Infrastructure.DomainEvents;
using XPL.Framework.Infrastructure.UnitOfWork;
using XPL.Framework.Domain.Model;
using System.Linq.Expressions;

namespace XPL.Framework.Infrastructure.Persistence
{
    public abstract class MappingRepository<TContext, TModel, TPersistence> : IRepository<TModel>
        where TModel : IEntity
        where TPersistence : class, ISqlId
        where TContext : DbContext
    {
        private readonly Func<DbSet<TPersistence>, IQueryable<TPersistence>>? _includes;

        protected abstract IModelConverter<TModel, TPersistence> Converter { get; }
        protected DbSet<TPersistence> DbSet { get; }

        private IDictionary<TModel, TPersistence> MaterializedObjects { get; }
        private IDictionary<long, TModel> MaterializedIds { get; }

        public MappingRepository(UnitOfWorkBase<TContext> uow, Func<TContext, DbSet<TPersistence>> dbSetSelector,
            Func<DbSet<TPersistence>, IQueryable<TPersistence>>? includes = null)
        {
            MaterializedObjects = new Dictionary<TModel, TPersistence>();
            MaterializedIds = new Dictionary<long, TModel>();
            DbSet = dbSetSelector(uow.DbContext);

            // Prepare to collaborate
            uow.OnSaving += OnSaving;
            uow.OnSaved += OnSaved;
            uow.RegisterDomainEventSource(this);
            _includes = includes;
        }

        public void Add(TModel obj)
        {
            var persisted = Converter.ToPersisted(obj);

            DbSet.Add(persisted);
            MaterializedObjects[obj] = persisted;
        }

        public void Delete(TModel obj)
        {
            if (!MaterializedObjects.ContainsKey(obj))
                throw new ArgumentException("Object is not materialized in this repository.");

            var persisted = MaterializedObjects[obj];
            DbSet.Remove(persisted);
            MaterializedObjects.Remove(obj);
        }

        public Option<TModel> TryFind(long id)
        {
            if (MaterializedIds.TryGetValue(id, out TModel materializedModel))
                return materializedModel;

            TPersistence? persisted = (_includes == null ? DbSet :_includes(DbSet)).Where(i => i.Id == id).SingleOrDefault();

            if (persisted == null)
                return None.Value;

            var model = Converter.ToModel(persisted);
            MaterializedObjects[model] = persisted;
            MaterializedIds[id] = model;

            return model;
        }

        private void OnSaving()
        {
            foreach (var kvp in MaterializedObjects)
                Converter.CopyChanges(kvp.Key, kvp.Value);
        }

        private void OnSaved()
        {
            foreach (var kvp in MaterializedObjects)
            {
                long id = kvp.Value.Id;
                MaterializedIds[id] = kvp.Key;
            }
        }

        IEnumerable<IDomainEvent> IDomainEventSource.GetEvents() =>
            MaterializedObjects.Keys.SelectMany(e => e.GetDomainEvents());
        void IDomainEventSource.ClearEvents() => MaterializedObjects.Keys.ToList().ForEach(e => e.ClearEvents());
    }
}
