using System;

namespace XPL.Framework.Domain
{
    public interface ISystemClock
    {
        DateTime UtcNow { get; }
        DateTime Now { get; }
    }
}
