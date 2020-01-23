using System;
using XPL.Modules.Kernel;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations.Statuses
{
    public sealed class Expired : Status
    {
        public override Status Confirm(string s, Action _) => throw new DomainException("Cannot Confirm and Expired registration.");
        public override Status Expire() => throw new DomainException("Registration is already expired.");
    }
}
