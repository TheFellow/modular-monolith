using System;
using XPL.Framework.Kernel.Email;
using XPL.Framework.Modules.Domain;
using XPL.Modules.UserAccess.Domain.Kernel;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations.Events
{
    public class UserRegistrationConfirmed : IDomainEvent
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
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
