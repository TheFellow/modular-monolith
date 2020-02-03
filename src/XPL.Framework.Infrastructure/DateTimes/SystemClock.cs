using System;
using XPL.Framework.Domain;

namespace XPL.Framework.Infrastructure.DateTimes
{
    public class SystemClock : ISystemClock
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public DateTime Now => DateTime.Now;
    }
}
