using System.Collections.Generic;
using ValueTypes;

namespace XPL.Modules.UserAccess.Domain.Kernel
{
    public class Password : Value
    {
        public string Value { get; }
        public Password(string password) => Value = password;

        protected override IEnumerable<ValueBase> GetValues() => Yield(Value);
    }
}
