using System;
using XPL.Framework.Application.Contracts;

namespace XPL.Modules.UserAccess.Application.UseCases.Registrations.ConfirmRegistration
{
    public class ConfirmRegistrationCommand : ICommand<string>
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string Login { get; }
        public string ConfirmationCode { get; }

        public ConfirmRegistrationCommand(string login, string confirmationCode)
        {
            Login = login;
            ConfirmationCode = confirmationCode;
        }
    }
}
