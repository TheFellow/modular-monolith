using System;
using XPL.Modules.UserAccess.Domain.Registrations;

namespace XPL.Modules.UserAccess.Application.UseCases.Registrations.NewUserRegistration
{
    public class NewRegistrationResponse
    {
        public string Login { get; }
        public Guid RegistrationId { get; }
        public DateTime ExpiryDate { get; }

        public NewRegistrationResponse(Registration registration, string login)
        {
            Login = login;
            RegistrationId = registration.RegistrationId.Id;
            ExpiryDate = registration.ExpiryDate;
        }
    }
}