using Lamar;
using Microsoft.EntityFrameworkCore;
using XPL.Framework.Application.Ports;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Framework.Infrastructure.UnitOfWork;
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

            var assembly = GetType().Assembly.GetName().Name;

            //For<IUnitOfWork>().Use<UserAccessUoW>().Scoped().Named(assembly);

            Use<UserAccessUoW>().Named(assembly)
                .For<IUnitOfWork>()
                .For<UnitOfWorkBase<UserAccessDbContext>>();


            For<UserRegistrationRepository>().Use<UserRegistrationRepository>().Scoped();
        }
    }
}
