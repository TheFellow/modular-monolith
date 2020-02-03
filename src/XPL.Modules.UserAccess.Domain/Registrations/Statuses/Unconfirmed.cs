using System;
using XPL.Framework.Domain;
using XPL.Modules.Kernel;

namespace XPL.Modules.UserAccess.Domain.Registrations.Statuses
{
    public sealed class Unconfirmed : Status
    {
        private readonly ISystemClock _systemClock;
        private readonly DateTime _expiryDate;
        private readonly string _confirmationCode;

        public Unconfirmed(ISystemClock systemClock, DateTime expiryDate, string confirmationCode)
        {
            _systemClock = systemClock;
            _expiryDate = expiryDate;
            _confirmationCode = confirmationCode;
        }

        public override Status Confirm(string confirmationCode, Action action)
        {
            if (string.IsNullOrWhiteSpace(confirmationCode))
                throw new ArgumentException(nameof(confirmationCode));

            if (!CanConfirm())
                throw new DomainException("Cannot confirm registration after expiration date.");

            if (confirmationCode != _confirmationCode)
                throw new DomainException("Incorrect confirmation code.");

            action();
            return new Confirmed();
        }
        public override Status Expire() => new Expired();

        private bool CanConfirm() => _systemClock.Now.Date <= _expiryDate;

    }
}
