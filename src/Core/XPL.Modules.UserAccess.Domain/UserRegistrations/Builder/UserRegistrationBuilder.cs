using Functional.Either;
using Functional.Option;
using System;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Rules;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations
{
    public partial class UserRegistration
    {
        public class UserRegistrationBuilder
        {
            private readonly LoginValidator _loginValidator;
            private string? _login;
            private string? _password;
            private string? _email;
            private string? _firstName;
            private string? _lastName;

            public UserRegistrationBuilder(LoginValidator loginValidator)
            {
                _loginValidator = loginValidator;
            }

            public UserRegistrationBuilder WithLogin(string login)
            {
                _login = login;
                return this;
            }

            public UserRegistrationBuilder WithPassword(string password)
            {
                _password = password;
                return this;
            }

            public UserRegistrationBuilder WithEmail(string email)
            {
                _email = email;
                return this;
            }

            public UserRegistrationBuilder WithFirstName(string firstName)
            {
                _firstName = firstName;
                return this;
            }

            public UserRegistrationBuilder WithLastName(string lastName)
            {
                _lastName = lastName;
                return this;
            }

            public Either<UserRegistrationError, UserRegistration> Build()
            {
                if (_login is null || _password is null || _email is null || _firstName is null || _lastName is null)
                    throw new InvalidOperationException("Specify all builder elements before attempting to build.");

                var login = new Login(_login);

                if (_loginValidator.ValidateLogin(login!) is Some<UserRegistrationError> loginError)
                    return loginError.Content;
                try
                {
                    return new UserRegistration()
                    {
                        Id = RegistrationId.New,
                        _login = login,
                        _password = new Password(_password),
                        _email = new Email(_email),
                        _firstName = new FirstName(_firstName),
                        _lastName = new LastName(_lastName)
                    };
                }
                catch (Exception ex)
                {
                    return new UserRegistrationError(ex.Message);
                }
            }
        }
    }
}
