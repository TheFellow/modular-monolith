using System;
using XPL.Modules.UserAccess.Domain.UserRegistrations;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.RegisterNewUser
{
    public class RegisterNewUserResponse
    {
        public Guid Id { get; }

        public RegisterNewUserResponse(UserRegistration registration) => Id = registration.RegistrationId.Id;
    }
}