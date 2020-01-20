﻿using Lamar;
using Microsoft.EntityFrameworkCore;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Rules;
using XPL.Modules.UserAccess.Infrastructure.Data;
using XPL.Modules.UserAccess.Infrastructure.UserRegistrations;
using XPL.Modules.UserAccess.Infrastructure.UserRegistrations.Rules;

namespace XPL.Modules.UserAccess.Application.Startup
{
    public class UserAccessServiceRegistry : ServiceRegistry
    {
        public UserAccessServiceRegistry()
        {
            For<ILoginExists>().Use<LoginExists>().Transient();

            For<UserAccessDbContext>().Use<UserAccessDbContext>()
                .Ctor<DbContextOptions<UserAccessDbContext>>().Is(ctx =>
                {
                    var connString = ctx.GetInstance<ConnectionString>();
                    return UserAccessContextOptions.GetOptions(connString);
                })
                .Scoped();

            For<UserRegistrationRepository>().Use<UserRegistrationRepository>().Scoped();
            For<UserAccessUoW>().Use<UserAccessUoW>().Scoped();
        }
    }
}
