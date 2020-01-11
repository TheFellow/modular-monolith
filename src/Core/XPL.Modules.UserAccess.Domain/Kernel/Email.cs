using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ValueTypes;

namespace XPL.Modules.UserAccess.Domain.Kernel
{

    public class Email : Value
    {
        public string Value { get; }
        public Email(string value)
        {
            ValidateEmailFormat(value);
            Value = value;
        }

        protected override IEnumerable<ValueBase> GetValues() => Yield(Value);
        private void ValidateEmailFormat(string value)
        {
            if (!Regex.IsMatch(value, @"\w+@\w+\.[a-zA-Z]{2,4}"))
                throw new ArgumentException("Email address is not in a valid format.");
        }
    }
}
