using System;

namespace XPL.Framework.Kernel.DateTimes
{
    public interface ISystemClock
    {
        DateTime UtcNow { get; }
        DateTime Now { get; }
    }
}
