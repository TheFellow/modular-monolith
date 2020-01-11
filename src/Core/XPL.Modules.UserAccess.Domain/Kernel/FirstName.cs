using System;
using System.Collections.Generic;
using ValueTypes;

namespace XPL.Modules.UserAccess.Domain.Kernel
{
    public class FirstName : Value
    {
        public string Value { get; }
        public FirstName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("First name cannot be empty.");
            Value = value;
        }

        protected override IEnumerable<ValueBase> GetValues() => Yield(Value);
    }
}
