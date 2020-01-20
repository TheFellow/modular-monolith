using Microsoft.EntityFrameworkCore;
using System;
using XPL.Framework.Infrastructure.UnitOfWork;
using XPL.Framework.Modules;
using XPL.Modules.UserAccess.Infrastructure.Data.Model;

namespace XPL.Modules.UserAccess.Infrastructure.Data
{
    public class UserAccessUoW : UnitOfWorkBase<UserAccessDbContext>
    {
        public UserAccessUoW(Func<UserAccessDbContext> dbContextFactory, IDomainEventDispatcher domainEventDispatcher)
            : base(dbContextFactory, domainEventDispatcher)
        {
            SqlUserRegistrations = DbContext.UserRegistrations;
        }

        internal DbSet<SqlUserRegistration> SqlUserRegistrations { get; }
    }
}
