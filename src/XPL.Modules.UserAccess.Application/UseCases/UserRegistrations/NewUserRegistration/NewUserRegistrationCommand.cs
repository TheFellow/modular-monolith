﻿using System;
using XPL.Framework.Domain.Contracts;

namespace XPL.Modules.UserAccess.Application.UseCases.UserRegistrations.NewUserRegistration
{
    public class NewUserRegistrationCommand : ICommand<NewUserRegistrationResponse>
    {
        public string Login { get; }
        public string Password { get; }
        public string Email { get; }
        public string FirstName { get; }
        public string LastName { get; }

        public Guid CorrelationId { get; } = Guid.NewGuid();

        public NewUserRegistrationCommand(string login, string password, string email, string firstName, string lastName)
        {
            Login = login;
            Password = password;
            Email = email;
            FirstName = firstName;
            LastName = lastName;
        }
    }
}
