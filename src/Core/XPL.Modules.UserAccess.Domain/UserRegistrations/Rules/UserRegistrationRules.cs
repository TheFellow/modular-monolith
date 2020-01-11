using Functional.Option;
using XPL.Modules.UserAccess.Domain.Kernel;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations.Rules
{
    public class UserRegistrationRules : IUserRegistrationRules
    {
        private readonly LoginValidator _loginValidator;

        public UserRegistrationRules(LoginValidator loginValidator)
        {
            _loginValidator = loginValidator;
        }

        public Option<UserRegistrationError> ValidateLogin(Login login) => _loginValidator.ValidateLogin(login);

    }
}