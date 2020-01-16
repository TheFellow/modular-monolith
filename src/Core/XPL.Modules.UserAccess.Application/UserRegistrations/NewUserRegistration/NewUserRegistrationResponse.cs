using System;
using XPL.Modules.UserAccess.Domain.UserRegistrations;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.NewUserRegistration
{
    public class NewUserRegistrationResponse
    {
        public string Login { get; }
        public Guid RegistrationId { get; }
        public DateTime ExpiryDate { get; }

        public NewUserRegistrationResponse(UserRegistration registration, string login)
        {
            Login = login;
            RegistrationId = registration.RegistrationId.Id;
            ExpiryDate = registration.ExpiryDate;
        }
    }
}