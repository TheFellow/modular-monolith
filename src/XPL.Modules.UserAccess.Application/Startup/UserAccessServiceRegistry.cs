using Lamar;
using Microsoft.EntityFrameworkCore;
using XPL.Framework.Application.Ports;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.UserAccess.Domain.Registrations.Rules;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Data;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Registrations;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;
using XPL.Modules.UserAccess.Infrastructure.Emails;
using XPL.Modules.UserAccess.Infrastructure.Registrations.Rules;
using static XPL.Modules.UserAccess.Infrastructure.Data.UserAccessContextOptions.TrackingBehavior;

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
                    return UserAccessContextOptions.GetOptions(connString, TrackAll);
                })
                .Scoped();

            For<IUserAccessQueryContext>().Use<UserAccessDbContext>()
                .Ctor<DbContextOptions<UserAccessDbContext>>().Is(ctx =>
                {
                    var connString = ctx.GetInstance<ConnectionString>();
                    return UserAccessContextOptions.GetOptions(connString, NoTracking);
                })
                .Scoped();

            string assemblyName = GetType().Assembly.GetName().Name;

            For<UserAccessUoW>().Use<UserAccessUoW>().Scoped();
            For<IUnitOfWork>().Use(ctx => ctx.GetInstance<UserAccessUoW>()).Named(assemblyName).Scoped();

            For<RegistrationRepository>().Use<RegistrationRepository>().Scoped();
            For<UserRepository>().Use<UserRepository>().Scoped();
        }
    }
}
