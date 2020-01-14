using System.Collections.Generic;
using ValueTypes;
using XPL.Framework.Modules.Domain;

namespace XPL.Modules.UserAccess.Domain.Kernel
{
    public class LastName : Value
    {
        public string Value { get; }
        public LastName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException("Last name cannot be empty.");
            Value = value;
        }

        protected override IEnumerable<ValueBase> GetValues() => Yield(Value);
    }
}
