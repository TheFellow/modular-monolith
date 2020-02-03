using Functional.Option;
using System;
using XPL.Framework.Domain;
using XPL.Modules.Kernel;
using XPL.Modules.Kernel.Email;
using XPL.Modules.Kernel.Passwords;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.Registrations.Rules;
using XPL.Modules.UserAccess.Domain.Registrations.Statuses;
using XPL.Modules.UserAccess.Domain.Users;
using static XPL.Modules.UserAccess.Domain.Registrations.Registration;

namespace XPL.Modules.UserAccess.Domain.Registrations
{
    public class Builder
    {
        private readonly LoginValidator _loginValidator;
        private readonly IEmailUsage _emailUsage;
        private readonly ISystemClock _systemClock;
        private string? _login;
        private string? _password;
        private string? _email;
        private string? _firstName;
        private string? _lastName;

        public Builder(LoginValidator loginValidator, IEmailUsage emailUsage, ISystemClock systemClock)
        {
            _loginValidator = loginValidator;
            _emailUsage = emailUsage;
            _systemClock = systemClock;
        }

        public Builder WithLogin(string login)
        {
            _login = login;
            return this;
        }

        public Builder WithPassword(string password)
        {
            _password = password;
            return this;
        }

        public Builder WithEmail(string email)
        {
            _email = email;
            return this;
        }

        public Builder WithFirstName(string firstName)
        {
            _firstName = firstName;
            return this;
        }

        public Builder WithLastName(string lastName)
        {
            _lastName = lastName;
            return this;
        }

        public Registration Build()
        {
            if (_login is null || _password is null || _email is null || _firstName is null || _lastName is null)
                throw new InvalidOperationException("Specify all builder elements before attempting to build.");

            var login = new Login(_login);

            if (_loginValidator.ValidateLogin(login) is Some<UserAccessError> loginError)
                throw new DomainException(loginError.Content.Message);

            if (_emailUsage.TryGetLoginForEmail(new EmailAddress(_email)) is Some<Login>)
                throw new DomainException("Email address is already in use.");

            DateTime expiryDate = _systemClock.Now.AddDays(7).Date;

            var password = new Password(_password);
            string confirmationCode = "abc123"; // TODO: Generate confirmation code

            var memento = new Memento(_email, _login, confirmationCode, password.HashedPassword, password.Salt,
                _firstName, _lastName, nameof(Unconfirmed), _systemClock.Now.Date, _systemClock, RegistrationId.New.Id, expiryDate);

            return memento.From();
        }
    }
}
