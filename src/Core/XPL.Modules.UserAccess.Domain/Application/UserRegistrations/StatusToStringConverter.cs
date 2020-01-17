using System;
using XPL.Framework.Kernel.DateTimes;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Statuses;

namespace XPL.Modules.UserAccess.Domain.Application.UserRegistrations
{
    public class StatusToStringConverter
    {
        private readonly ISystemClock _systemClock;

        public StatusToStringConverter(ISystemClock systemClock) => _systemClock = systemClock;

        public Status ToStatus(string status, DateTime expiryDate) => status switch
        {
            nameof(Unconfirmed) => new Unconfirmed(_systemClock, expiryDate),
            nameof(Confirmed) => new Confirmed(),
            nameof(Expired) => new Expired(),
            _ => throw new InvalidOperationException()
        };

        public string ToString(Status status) => status.GetType().Name;
    }
}
