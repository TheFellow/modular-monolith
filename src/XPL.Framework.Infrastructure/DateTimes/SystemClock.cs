using System;
using XPL.Modules.Kernel.DateTimes;

namespace XPL.Framework.Kernel.DateTimes
{
    public class SystemClock : ISystemClock
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public DateTime Now => DateTime.Now;
    }
}
