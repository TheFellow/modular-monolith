using System;
using XPL.Framework.Kernel.DateTimes;
using XPL.Framework.Modules.Domain;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations.Statuses
{
    public sealed class Confirmed : Status
    {
        public override Status Confirm(Action action) => throw new DomainException("Registration is already confirmed.");
        public override Status Expire() => throw new DomainException("Cannot Expire a Confirmed registration.");
    }
}
