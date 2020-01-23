using System;
using XPL.Modules.Kernel;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations.Statuses
{
    public sealed class Confirmed : Status
    {
        public override Status Confirm(string s, Action _) => throw new DomainException("Registration is already confirmed.");
        public override Status Expire() => throw new DomainException("Cannot Expire a Confirmed registration.");
    }
}
