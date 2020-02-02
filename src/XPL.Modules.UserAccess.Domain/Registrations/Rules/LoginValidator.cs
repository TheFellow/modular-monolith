using Functional.Option;
using XPL.Modules.UserAccess.Domain.Kernel;

namespace XPL.Modules.UserAccess.Domain.Registrations.Rules
{
    public class LoginValidator
    {
        private readonly ILoginExists _loginExists;

        public LoginValidator(ILoginExists loginExists) => _loginExists = loginExists;

        public Option<UserAccessError> ValidateLogin(Login login)
        {
            if (_loginExists.LoginExists(login))
                return new UserAccessError($"Login \"{login.Value}\" already exists.");

            return None.Value;
        }
    }
}
