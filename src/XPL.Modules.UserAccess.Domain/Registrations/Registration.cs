using Functional.Either;
using System;
using XPL.Framework.Domain.Model;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.Registrations.Events;
using XPL.Modules.UserAccess.Domain.Registrations.Statuses;
using XPL.Modules.Kernel.Passwords;
using XPL.Modules.Kernel.Email;
using XPL.Modules.Kernel.DateTimes;

namespace XPL.Modules.UserAccess.Domain.Registrations
{
    public partial class Registration : Entity
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

        public void Confirm(string confirmationCode)
        {
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
            });
        }

        public void Expire() => _status = _status.Expire();
    }
}
