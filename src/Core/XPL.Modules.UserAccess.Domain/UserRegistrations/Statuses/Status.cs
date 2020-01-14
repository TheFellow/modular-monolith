using System;
using XPL.Framework.Kernel.DateTimes;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations.Statuses
{
    public abstract class Status
    {
        public abstract Status Confirm(Action action);
        public abstract Status Expire();
    }
}
