using Microsoft.EntityFrameworkCore;
using System;
using XPL.Framework.Application.Ports.Bus;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Framework.Infrastructure.UnitOfWork;
using XPL.Framework.Modules;
using XPL.Modules.UserAccess.Domain.UserRegistrations;
using XPL.Modules.UserAccess.Infrastructure.Data.Model;

namespace XPL.Modules.UserAccess.Infrastructure.Data
{
    public class UserAccessUoW : UnitOfWorkBase<UserAccessDbContext>
    {
        public UserAccessUoW(Func<UserAccessDbContext> dbContextFactory, IBus bus, IDomainEventDispatcher domainEventDispatcher)
            : base(dbContextFactory, bus, domainEventDispatcher)
        {
            SqlUserRegistrations = DbContext.UserRegistrations;
        }

        internal DbSet<SqlUserRegistration> SqlUserRegistrations { get; }
    }
}
