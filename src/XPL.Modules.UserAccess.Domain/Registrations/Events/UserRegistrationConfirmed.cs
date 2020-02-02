using System;
using XPL.Framework.Domain.Model;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.Kernel.Passwords;
using XPL.Modules.Kernel.Email;

namespace XPL.Modules.UserAccess.Domain.Registrations.Events
{
    public sealed class UserRegistrationConfirmed : IDomainEvent
    {
        public Guid CorrelationId { get; set; }
        public RegistrationId RegistrationId { get; }
        public EmailAddress Email { get; }
        public Login Login { get; }
        public Password Password { get; }
        public FirstName FirstName { get; }
        public LastName LastName { get; }

        public UserRegistrationConfirmed(
            RegistrationId registrationId,
            EmailAddress email,
            Login login,
            Password password,
            FirstName firstName,
            LastName lastName)
        {
            RegistrationId = registrationId;
            Email = email;
            Login = login;
            Password = password;
            FirstName = firstName;
            LastName = lastName;
        }
    }
}
