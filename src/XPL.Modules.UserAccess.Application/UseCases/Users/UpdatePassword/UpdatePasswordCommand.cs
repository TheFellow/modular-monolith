using System;
using XPL.Framework.Application.Contracts;

namespace XPL.Modules.UserAccess.Application.UseCases.Users.UpdatePassword
{
    public class UpdatePasswordCommand : ICommand<CommandResult>
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string Login { get; }
        public string OldPassword { get; }
        public string NewPassword { get; }

        public UpdatePasswordCommand(string login, string oldPassword, string newPassword)
        {
            Login = login;
            OldPassword = oldPassword;
            NewPassword = newPassword;
        }
    }
}
