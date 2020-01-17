﻿using System;
using XPL.Framework.Kernel;
using XPL.Framework.Kernel.DateTimes;

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
            if (!CanConfirm())
                throw new DomainException("Cannot confirm registration after expiration date.");

            action();
            return new Confirmed();
        }
        public override Status Expire() => new Expired();

        private bool CanConfirm() => _systemClock.Now.Date <= _expiryDate;

    }
}
