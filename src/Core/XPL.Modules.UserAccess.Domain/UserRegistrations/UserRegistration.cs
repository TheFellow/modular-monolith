using System;
using XPL.Framework.Kernel.DateTimes;
using XPL.Framework.Kernel.Email;
using XPL.Framework.Modules.Domain;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Events;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Statuses;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations
{
    public partial class UserRegistration : Entity
    {
        private readonly EmailAddress _email;
        private readonly Login _login;
        private readonly Password _password;
        private readonly FirstName _firstName;
        private readonly LastName _lastName;

        private readonly ISystemClock _systemClock;
        private readonly string _confirmationCode;
        private Status _status;

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

    }
}
