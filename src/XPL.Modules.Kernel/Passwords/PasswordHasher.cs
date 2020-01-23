﻿using System;
using System.Security.Cryptography;

namespace XPL.Framework.Kernel.Passwords
{
    public class PasswordHasher
    {
        public class HashSalt
        {
            public string Hash { get; set; }
            public string Salt { get; set; }
            public HashSalt(string hash, string salt)
            {
                Hash = hash;
                Salt = salt;
            }
        }

        public static HashSalt Create(string password)
        {
            var saltBytes = new byte[64];
            var provider = new RNGCryptoServiceProvider();
            provider.GetNonZeroBytes(saltBytes);
            var salt = Convert.ToBase64String(saltBytes);

            var deriveBytes = new Rfc2898DeriveBytes(password, saltBytes);
            var hashPassword = Convert.ToBase64String(deriveBytes.GetBytes(256));

            return new HashSalt(hashPassword, salt);
        }

    }
}