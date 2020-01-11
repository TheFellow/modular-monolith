using Functional.Option;
using XPL.Modules.UserAccess.Domain.Kernel;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations
{
    public interface IUserRegistrationRules
    {
        Option<UserRegistrationError> ValidateLogin(Login login);
    }
}