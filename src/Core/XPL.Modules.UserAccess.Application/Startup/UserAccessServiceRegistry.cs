using Lamar;
using Microsoft.EntityFrameworkCore;
using XPL.Framework.Application.Ports;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Rules;
using XPL.Modules.UserAccess.Infrastructure.Data;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.UserRegistrations;
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

            string assemblyName = GetType().Assembly.GetName().Name;

            For<UserAccessUoW>().Use<UserAccessUoW>().Scoped();
            For<IUnitOfWork>().Use(ctx => ctx.GetInstance<UserAccessUoW>()).Named(assemblyName);

            For<UserRegistrationRepository>().Use<UserRegistrationRepository>().Scoped();
        }
    }
}
