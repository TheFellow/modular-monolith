using System;
using System.Collections.Generic;
using ValueTypes;

namespace XPL.Modules.UserAccess.Domain.Kernel
{
    public class Login : Value
    {
        public string Value { get; }

        public Login(string login)
        {
            if (string.IsNullOrWhiteSpace(login))
                throw new ArgumentException("Login name cannot be empty");
            Value = login;
        }

        protected override IEnumerable<ValueBase> GetValues() => Yield(Value);
    }
}