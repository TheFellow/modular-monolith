using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Rules;

namespace XPL.Modules.UserAccess.Infrastructure.UserRegistrations.Rules
{
    public class LoginExists : ILoginExists
    {
        bool ILoginExists.LoginExists(Login login)
        {
            // Stubbed for now. Alice is the only registration.
            if (login.Value.ToLowerInvariant() == "alice")
                return true;

            return false;
        }
    }
}
