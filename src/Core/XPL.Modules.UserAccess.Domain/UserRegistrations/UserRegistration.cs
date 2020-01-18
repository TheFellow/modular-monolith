using Functional.Either;
using System;
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
        private readonly EmailAddress _email;
        private readonly Login _login;
        private readonly Password _password;
        private readonly FirstName _firstName;
        private readonly LastName _lastName;
        private readonly string _confirmationCode;
        private readonly ISystemClock _systemClock;

        private Status _status;

        public RegistrationId RegistrationId { get; private set; }
        public DateTime ExpiryDate { get; private set; }

        public Either<InvalidConfirmationCode, UserRegistration> Confirm(string confirmationCode)
        {
            Either<InvalidConfirmationCode, UserRegistration> result = this;

            _status = _status.Confirm(() =>
            {
                if (string.IsNullOrWhiteSpace(confirmationCode))
                    throw new ArgumentException(nameof(confirmationCode));

                if (confirmationCode != _confirmationCode)
                {
                    result = new InvalidConfirmationCode();
                    return;
                }

                AddDomainEvent(new UserRegistrationConfirmed(
                    RegistrationId,
                    _email,
                    _login,
                    _password,
                    _firstName,
                    _lastName));
            });

            return result;
        }

        public void Expire() => _status = _status.Expire();
    }
}
