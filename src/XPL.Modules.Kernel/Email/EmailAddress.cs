using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ValueTypes;

namespace XPL.Modules.Kernel.Email
{

    public class EmailAddress : Value
    {
        public string Value { get; }
        public EmailAddress(string value)
        {
            if (!IsValid(value))
                throw new ArgumentException($"Email address '{value}' is invalid.");
            Value = value;
        }

        protected override IEnumerable<ValueBase> GetValues() => Yield(Value);

        public static bool IsValid(string email) => Regex.IsMatch(email, @"\w+@\w+\.[a-zA-Z]{2,4}");
    }
}
