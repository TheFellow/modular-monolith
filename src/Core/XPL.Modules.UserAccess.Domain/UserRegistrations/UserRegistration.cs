using System;
using XPL.Framework.Kernel;
using XPL.Framework.Kernel.DateTimes;
using XPL.Framework.Kernel.Email;
using XPL.Framework.Kernel.Passwords;
using XPL.Framework.Modules.Domain;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Events;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Statuses;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations
{
    public partial class UserRegistration : Entity
    {
        private EmailAddress _email;
        private Login _login;
        private Password _password;
        private FirstName _firstName;
        private LastName _lastName;
        private string _confirmationCode;
        private Status _status;
        private ISystemClock _systemClock;

        public RegistrationId RegistrationId { get; private set; }
        public DateTime ExpiryDate { get; private set; }

        public void Confirm(string confirmationCode)
        {
            _status = _status.Confirm(() =>
            {
                if (string.IsNullOrWhiteSpace(confirmationCode) || confirmationCode != _confirmationCode)
                    throw new DomainException("Confirmation code is invalid.");

                AddDomainEvent(new UserRegistrationConfirmed(
                    RegistrationId,
                    _email,
                    _login,
                    _password,
                    _firstName,
                    _lastName));
            });
        }

        public void Expire() => _status = _status.Expire();

        private UserRegistration(
            ISystemClock systemClock,
            RegistrationId registrationId,
            EmailAddress email,
            Login login,
            Password password,
            FirstName firstName,
            LastName lastName,
            string confirmationCode,
            DateTime expiryDate)
        {
            _systemClock = systemClock;
            RegistrationId = registrationId;
            _email = email;
            _login = login;
            _password = password;
            _firstName = firstName;
            _lastName = lastName;
            _confirmationCode = confirmationCode;
            ExpiryDate = expiryDate;
            _status = new Unconfirmed(_systemClock, ExpiryDate);
        }

        #region Private Empty Ctor
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        private UserRegistration() { }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable. 
        #endregion
    }
}
