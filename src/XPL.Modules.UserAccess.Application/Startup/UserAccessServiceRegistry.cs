﻿using Lamar;
using Microsoft.EntityFrameworkCore;
using XPL.Framework.Application.Ports;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Rules;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Data;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.UserRegistrations;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;
using XPL.Modules.UserAccess.Infrastructure.Emails;
using XPL.Modules.UserAccess.Infrastructure.UserRegistrations.Rules;
using static XPL.Modules.UserAccess.Infrastructure.Data.UserAccessContextOptions;

namespace XPL.Modules.UserAccess.Application.Startup
{
    public class UserAccessServiceRegistry : ServiceRegistry
    {
        public UserAccessServiceRegistry()
        {
            For<ILoginExists>().Use<LoginExists>().Transient();
            For<IEmailUsage>().Use<EmailUsage>().Transient();

            For<UserAccessDbContext>().Use<UserAccessDbContext>()
                .Ctor<DbContextOptions<UserAccessDbContext>>().Is(ctx =>
                {
                    var connString = ctx.GetInstance<ConnectionString>();
                    return UserAccessContextOptions.GetOptions(connString, TrackingBehavior.TrackAll);
                })
                .Scoped();

            For<IUserAccessQueryContext>().Use<UserAccessDbContext>()
                .Ctor<DbContextOptions<UserAccessDbContext>>().Is(ctx =>
                {
                    var connString = ctx.GetInstance<ConnectionString>();
                    return UserAccessContextOptions.GetOptions(connString, TrackingBehavior.NoTracking);
                })
                .Scoped();

            string assemblyName = GetType().Assembly.GetName().Name;

            For<UserAccessUoW>().Use<UserAccessUoW>().Scoped();
            For<IUnitOfWork>().Use(ctx => ctx.GetInstance<UserAccessUoW>()).Named(assemblyName).Scoped();

            For<UserRegistrationRepository>().Use<UserRegistrationRepository>().Scoped();
            For<UserRepository>().Use<UserRepository>().Scoped();
        }
    }
}
