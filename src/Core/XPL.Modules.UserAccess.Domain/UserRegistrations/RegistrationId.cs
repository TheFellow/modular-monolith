using System;
using System.Collections.Generic;
using ValueTypes;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations
{
    public class RegistrationId : Value
    {
        public Guid Id { get; }
        public RegistrationId(Guid id) => Id = id;
        protected override IEnumerable<ValueBase> GetValues() => Yield(Id);

        public static RegistrationId New => new RegistrationId(Guid.NewGuid());
    }
}
