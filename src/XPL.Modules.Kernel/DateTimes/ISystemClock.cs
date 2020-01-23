using System;

namespace XPL.Modules.Kernel.DateTimes
{
    public interface ISystemClock
    {
        DateTime UtcNow { get; }
        DateTime Now { get; }
    }
}
