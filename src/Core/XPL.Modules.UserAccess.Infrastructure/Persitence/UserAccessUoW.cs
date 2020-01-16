using System;
using XPL.Framework.Application.Ports.Bus;
using XPL.Framework.Infrastructure.UnitOfWork;
using XPL.Framework.Modules;

namespace XPL.Modules.UserAccess.Infrastructure.Persitence
{
    public class UserAccessUoW : UnitOfWorkBase<UserAccessDbContext>
    {
        public UserAccessUoW(Func<UserAccessDbContext> dbContextFactory, IBus bus, IDomainEventDispatcher domainEventDispatcher)
            : base(dbContextFactory, bus, domainEventDispatcher)
        {
        }
    }
}
