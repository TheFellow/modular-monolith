using System;
using XPL.Framework.Application.Contracts;
using XPL.Framework.Application.Contracts.Security;
using XPL.Modules.Kernel.Security;

namespace XPL.Modules.UserAccess.Application.UseCases.Users.RevokeRole
{
    [Authorize(Roles = Roles.AdminRoleValue)]
    public class RevokeRoleCommand : ICommand<string>
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string Login { get; }
        public string Role { get; }

        public RevokeRoleCommand(string login, string role)
        {
            Login = login;
            Role = role;
        }
    }
}
