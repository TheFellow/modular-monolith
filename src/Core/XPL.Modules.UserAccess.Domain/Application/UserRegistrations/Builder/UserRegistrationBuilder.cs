using Functional.Either;
using Functional.Option;
using System;
using XPL.Framework.Kernel.DateTimes;
using XPL.Framework.Kernel.Email;
using XPL.Modules.UserAccess.Application.UserRegistrations.Builder;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Rules;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations
{
    public partial class UserRegistration
    {
        public class UserRegistrationBuilder : IUserRegistrationBuilder
        {
            private readonly LoginValidator _loginValidator;
            private readonly ISystemClock _systemClock;
            private string? _login;
            private string? _password;
            private string? _email;
            private string? _firstName;
            private string? _lastName;

            public UserRegistrationBuilder(LoginValidator loginValidator, ISystemClock systemClock)
            {
                _loginValidator = loginValidator;
                _systemClock = systemClock;
            }

            IUserRegistrationBuilder IUserRegistrationBuilder.WithLogin(string login)
            {
                _login = login;
                return this;
            }

            IUserRegistrationBuilder IUserRegistrationBuilder.WithPassword(string password)
            {
                _password = password;
                return this;
            }

            IUserRegistrationBuilder IUserRegistrationBuilder.WithEmail(string email)
            {
                _email = email;
                return this;
            }

            IUserRegistrationBuilder IUserRegistrationBuilder.WithFirstName(string firstName)
            {
                _firstName = firstName;
                return this;
            }

            IUserRegistrationBuilder IUserRegistrationBuilder.WithLastName(string lastName)
            {
                _lastName = lastName;
                return this;
            }

            Either<UserRegistrationError, UserRegistration> IUserRegistrationBuilder.Build()
            {
                if (_login is null || _password is null || _email is null || _firstName is null || _lastName is null)
                    throw new InvalidOperationException("Specify all builder elements before attempting to build.");

                var login = new Login(_login);

                if (_loginValidator.ValidateLogin(login) is Some<UserRegistrationError> loginError)
                    return loginError.Content;

                DateTime expiryDate = _systemClock.Now.AddDays(7).Date;

                return new UserRegistration(
                    _systemClock,
                    RegistrationId.New,
                    new EmailAddress(_email),
                    login,
                    new Password(_password),
                    new FirstName(_firstName),
                    new LastName(_lastName),
                    "abc123", // TODO: Generate a random confirmation code
                    expiryDate
                );
            }
        }
    }
}
