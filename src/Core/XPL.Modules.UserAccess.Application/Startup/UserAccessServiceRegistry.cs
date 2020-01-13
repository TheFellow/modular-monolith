using Lamar;
using XPL.Modules.UserAccess.Application.UserRegistrations.Builder;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Rules;
using XPL.Modules.UserAccess.Infrastructure.UserRegistrations.Rules;
using static XPL.Modules.UserAccess.Domain.UserRegistrations.UserRegistration;

namespace XPL.Modules.UserAccess.Application.Startup
{
    public class UserAccessServiceRegistry : ServiceRegistry
    {
        public UserAccessServiceRegistry()
        {
            For<IUserRegistrationBuilder>().Use<UserRegistrationBuilder>().Transient();
            For<ILoginExists>().Use<LoginExists>().Transient();
        }
    }
}
