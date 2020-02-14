using System;
using XPL.Framework.Application.Contracts;
using XPL.Framework.Application.Contracts.Security;
using XPL.Modules.Kernel.Security;

namespace XPL.Modules.UserAccess.Application.UseCases.Users.GrantRole
{
    [Authorize(Roles = Roles.AdminRoleValue)]
    public class GrantRoleCommand : ICommand<string>
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string Login { get; }
        public string Role { get; }

        public GrantRoleCommand(string login, string role)
        {
            Login = login;
            Role = role;
        }
    }
}
