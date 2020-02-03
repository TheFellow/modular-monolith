using System;
using XPL.Framework.Infrastructure.UnitOfWork;
using XPL.Framework.Infrastructure;

namespace XPL.Modules.UserAccess.Infrastructure.Data
{
    public class UserAccessUoW : UnitOfWorkBase<UserAccessDbContext>
    {
        public UserAccessUoW(Func<UserAccessDbContext> dbContextFactory, IDomainEventDispatcher domainEventDispatcher)
            : base(dbContextFactory, domainEventDispatcher)
        {
            
        }
    }
}
