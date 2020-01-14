using System;
using XPL.Framework.Kernel.DateTimes;
using XPL.Framework.Modules.Domain;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations.Statuses
{
    public sealed class Unconfirmed : Status
    {
        private readonly ISystemClock _systemClock;
        private readonly DateTime _expiryDate;

        public Unconfirmed(ISystemClock systemClock, DateTime expiryDate)
        {
            _systemClock = systemClock;
            _expiryDate = expiryDate;
        }

        public override Status Confirm(Action action)
        {
            if (_systemClock.Now.Date > _expiryDate)
                throw new DomainException("Cannot confirm registration after expiration date.");

            action();
            return new Confirmed();
        }

        public override Status Expire() => new Expired();
    }
}
