using System;
using XPL.Framework.Kernel.DateTimes;
using XPL.Framework.Kernel.Email;
using XPL.Framework.Modules.Domain;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Statuses;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations
{
    public partial class UserRegistration
    {
        private EmailAddress _email;
        private Login _login;
        private Password _password;
        private FirstName _firstName;
        private LastName _lastName;
        private string _confirmationCode;

        private Status _status;

        public DateTime ExpiryDate { get; private set; }

        public RegistrationId RegistrationId { get; private set; }

        private ISystemClock _systemClock;

        public void Confirm(string confirmationCode)
        {
            _status = _status.Confirm(() =>
            {
                if (string.IsNullOrWhiteSpace(confirmationCode) || confirmationCode != _confirmationCode)
                    throw new DomainException("Confirmation code is invalid");

                // TODO: Publish an event
            });
        }

        public void Expire() => _status = _status.Expire();

        #region Private parameterless ctor
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        private UserRegistration() { }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable. 
        #endregion
    }
}
