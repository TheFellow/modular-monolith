using System.Collections.Generic;
using ValueTypes;
using XPL.Framework.Modules.Domain;

namespace XPL.Modules.UserAccess.Domain.Kernel
{
    public class Login : Value
    {
        public string Value { get; }

        public Login(string login)
        {
            if (string.IsNullOrWhiteSpace(login))
                throw new DomainException("Login name cannot be empty");
            Value = login;
        }

        protected override IEnumerable<ValueBase> GetValues() => Yield(Value);
    }
}