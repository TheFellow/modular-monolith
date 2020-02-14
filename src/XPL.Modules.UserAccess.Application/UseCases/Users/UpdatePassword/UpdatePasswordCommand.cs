using System;
using XPL.Framework.Application.Contracts;
using XPL.Framework.Application.Contracts.Security;

namespace XPL.Modules.UserAccess.Application.UseCases.Users.UpdatePassword
{
    [Authorize(Login = nameof(Login))]
    public class UpdatePasswordCommand : ICommand<string>
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
