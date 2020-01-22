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
        private DateTime _statusDate;

        public RegistrationId RegistrationId { get; private set; }
        public DateTime ExpiryDate { get; private set; }

        public Either<InvalidConfirmationCode, UserRegistration> Confirm(string confirmationCode)
        {
            Either<InvalidConfirmationCode, UserRegistration> result = new InvalidConfirmationCode();

            _status = _status.Confirm(confirmationCode, () =>
            {
                _statusDate = _systemClock.Now.Date;

                AddDomainEvent(new UserRegistrationConfirmed(
                    RegistrationId,
                    _email,
                    _login,
                    _password,
                    _firstName,
                    _lastName));

                result = this;
            });

            return result;
        }

        public void Expire() => _status = _status.Expire();
    }
}
