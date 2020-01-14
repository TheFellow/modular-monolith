using System;
using XPL.Modules.UserAccess.Domain.UserRegistrations;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.NewUserRegistration
{
    public class NewUserRegistrationResponse
    {
        public Guid RegistrationId { get; }
        public DateTime ExpiryDate { get; }

        public NewUserRegistrationResponse(UserRegistration registration)
        {
            RegistrationId = registration.RegistrationId.Id;
            ExpiryDate = registration.ExpiryDate;
        }
    }
}