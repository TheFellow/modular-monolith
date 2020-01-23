﻿using System.Collections.Generic;
using ValueTypes;

namespace XPL.Framework.Kernel.Passwords
{

    public class Password : Value
    {
        public string HashedPassword { get; }
        public string Salt { get; }
        public Password(string password, PasswordComplexityRules complexityRules)
        {
            complexityRules.EnforceComplexity(password);
            var hashSalt = PasswordHasher.Create(password);

            HashedPassword = hashSalt.Hash;
            Salt = hashSalt.Salt;
        }
        public Password(string password) : this(password, PasswordComplexityRules.Default) { }

        public static Password Raw(string hashPassword, string salt) => new Password(hashPassword, salt);
        private Password(string hashPassword, string salt)
        {
            HashedPassword = hashPassword;
            Salt = salt;
        }

        protected override IEnumerable<ValueBase> GetValues() => Yield(HashedPassword, Salt);
    }
}