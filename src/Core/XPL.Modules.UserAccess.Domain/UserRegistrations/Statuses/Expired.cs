using System;
using XPL.Framework.Modules.Domain;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations.Statuses
{
    public sealed class Expired : Status
    {
        public override Status Confirm(Action action) => throw new DomainException("Cannot Confirm and Expired registration.");
        public override Status Expire() => throw new DomainException("Registration is already expired.");
    }
}
