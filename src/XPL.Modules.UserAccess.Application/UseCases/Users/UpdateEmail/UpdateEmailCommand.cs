using System;
using XPL.Framework.Application.Contracts;

namespace XPL.Modules.UserAccess.Application.UseCases.Users.UpdateEmail
{
    public class UpdateEmailCommand : ICommand<CommandResult>
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
