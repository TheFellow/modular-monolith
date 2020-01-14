using System.Collections.Generic;
using ValueTypes;

namespace XPL.Modules.UserAccess.Domain.Kernel
{
    public class Password : Value
    {
        public string Value { get; }
        public Password(string password, PasswordComplexityRules passwordComplexityRules)
        {
            passwordComplexityRules.EnforceComplexity(password);
            Value = password;
        }

        public Password(string password) : this(password, PasswordComplexityRules.Default) { }

        protected override IEnumerable<ValueBase> GetValues() => Yield(Value);
    }
}
