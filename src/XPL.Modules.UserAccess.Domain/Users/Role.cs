using System.Collections.Generic;
using ValueTypes;

namespace XPL.Modules.UserAccess.Domain.Users
{

    public class Role : Value
    {
        public string Value { get; }
        public Role(string value) => Value = value;

        protected override IEnumerable<ValueBase> GetValues() => Yield(Value);

        public static Role Member => new Role("Member");
    }
}
