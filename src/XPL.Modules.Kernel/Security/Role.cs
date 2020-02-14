using System.Collections.Generic;
using ValueTypes;

namespace XPL.Modules.Kernel.Security
{
    public sealed class Role : Value
    {
        public string Value { get; }
        public Role(string value) => Value = value;

        protected override IEnumerable<ValueBase> GetValues() => Yield(Value);
    }
}
