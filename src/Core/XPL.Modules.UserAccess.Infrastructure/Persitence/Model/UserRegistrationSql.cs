﻿using System;

namespace XPL.Modules.UserAccess.Infrastructure.Persitence.Model
{
    public class UserRegistrationSql
    {
        public UserRegistrationSql(
            int id,
            Guid registrationId,
            string login,
            string confirmationCode,
            string passwordHash,
            string passwordSalt,
            string firstName,
            string lastName,
            string updatedBy,
            DateTime updatedOn)
        {
            Id = id;
            RegistrationId = registrationId;
            Login = login;
            ConfirmationCode = confirmationCode;
            PasswordHash = passwordHash;
            PasswordSalt = passwordSalt;
            FirstName = firstName;
            LastName = lastName;
            UpdatedBy = updatedBy;
            UpdatedOn = updatedOn;
        }

        public int Id { get; set; }
        public Guid RegistrationId { get; set; }
        public string Login { get; set; }
        public string ConfirmationCode { get; set; }
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
