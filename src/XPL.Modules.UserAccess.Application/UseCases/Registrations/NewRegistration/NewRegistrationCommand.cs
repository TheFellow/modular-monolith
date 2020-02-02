using System;
using XPL.Framework.Application.Contracts;

namespace XPL.Modules.UserAccess.Application.UseCases.Registrations.NewUserRegistration
{
    public class NewRegistrationCommand : ICommand<NewRegistrationResponse>
    {
        public string Login { get; }
        public string Password { get; }
        public string Email { get; }
        public string FirstName { get; }
        public string LastName { get; }

        public Guid CorrelationId { get; } = Guid.NewGuid();

        public NewRegistrationCommand(string login, string password, string email, string firstName, string lastName)
        {
            Login = login;
            Password = password;
            Email = email;
            FirstName = firstName;
            LastName = lastName;
        }
    }
}
