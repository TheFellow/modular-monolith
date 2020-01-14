using System;
using XPL.Modules.UserAccess.Domain.UserRegistrations;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.RegisterNewUser
{
    public class RegisterNewUserResponse
    {
        public Guid RegistrationId { get; }
        public DateTime ExpiryDate { get; }

        public RegisterNewUserResponse(UserRegistration registration)
        {
            RegistrationId = registration.RegistrationId.Id;
            ExpiryDate = registration.ExpiryDate;
        }
    }
}