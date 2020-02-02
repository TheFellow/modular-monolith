using System;
using XPL.Framework.Application.Contracts;

namespace XPL.Modules.UserAccess.Application.UseCases.Registrations.ConfirmRegistration
{
    public class ConfirmRegistrationCommand : ICommand<string>
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid RegistrationId { get; }
        public string ConfirmationCode { get; }

        public ConfirmRegistrationCommand(Guid registrationId, string confirmationCode)
        {
            RegistrationId = registrationId;
            ConfirmationCode = confirmationCode;
        }
    }
}
