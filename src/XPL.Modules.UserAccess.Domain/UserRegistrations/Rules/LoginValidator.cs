using Functional.Option;
using XPL.Modules.UserAccess.Domain.Kernel;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations.Rules
{
    public class LoginValidator
    {
        private readonly ILoginExists _loginExists;

        public LoginValidator(ILoginExists loginExists) => _loginExists = loginExists;

        public Option<UserRegistrationError> ValidateLogin(Login login)
        {
            if (_loginExists.LoginExists(login))
                return new UserRegistrationError($"Login \"{login.Value}\" already exists.");

            return None.Value;
        }
    }
}
