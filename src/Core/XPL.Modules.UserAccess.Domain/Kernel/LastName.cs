using System;
using System.Collections.Generic;
using ValueTypes;

namespace XPL.Modules.UserAccess.Domain.Kernel
{
    public class LastName : Value
    {
        public string Value { get; }
        public LastName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Last name cannot be empty.");
            Value = value;
        }

        protected override IEnumerable<ValueBase> GetValues() => Yield(Value);
    }
}
