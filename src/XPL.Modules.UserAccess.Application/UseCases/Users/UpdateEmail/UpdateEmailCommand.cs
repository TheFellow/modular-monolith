using System;
using XPL.Framework.Application.Contracts;
using XPL.Framework.Application.Contracts.Security;

namespace XPL.Modules.UserAccess.Application.UseCases.Users.UpdateEmail
{
    [Authorize(Login = nameof(Login))]
    public class UpdateEmailCommand : ICommand<string>
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string Login { get; }
        public string NewEmail { get; }

        public UpdateEmailCommand(string login, string newEmail)
        {
            Login = login;
            NewEmail = newEmail;
        }
    }
}
