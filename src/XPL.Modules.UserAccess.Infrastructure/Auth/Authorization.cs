using Functional.Option;
using System;
using System.Linq;
using System.Security.Claims;
using XPL.Framework.Application.Contracts;
using XPL.Framework.Application.Contracts.Security;
using XPL.Framework.Application.Ports;
using XPL.Framework.Domain;
using XPL.Modules.Kernel.Security;

namespace XPL.Modules.UserAccess.Infrastructure.Auth
{
    public class Authorization : IAuthorization
    {
        private readonly IUserInfo _userInfo;
        public Authorization(IUserInfo userInfo) => _userInfo = userInfo;

        public void Authorize<TResult>(ICommand<TResult> command)
        {
            if (_userInfo.Identity.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == Roles.AdminRole))
                return;

            var option = command.GetType()
                .GetCustomAttributes(typeof(AuthorizeAttribute), false)
                .OfType<AuthorizeAttribute>()
                .FirstOrNone();

            if (!(option is Some<AuthorizeAttribute> some))
                return;

            var auth = some.Content;

            AuthorizeLogin(auth, command);
            AuthorizeRoles(auth, command);
        }

        private void AuthorizeLogin<TResult>(AuthorizeAttribute auth, ICommand<TResult> command)
        {
            if (string.IsNullOrWhiteSpace(auth.Login))
                return;

            var pi = command.GetType().GetProperty(auth.Login);
            string commandLogin = (string)pi.GetValue(command);

            if (!string.Equals(commandLogin, _userInfo.Identity.Name, StringComparison.InvariantCultureIgnoreCase))
                throw new UnauthorizedException();
        }

        private void AuthorizeRoles<TResult>(AuthorizeAttribute auth, ICommand<TResult> command)
        {
            if (string.IsNullOrWhiteSpace(auth.Roles))
                return;

            foreach (string role in auth.Roles.Split(','))
            {
                if (_userInfo.Identity.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == role.Trim()))
                    return;
            }

            throw new UnauthorizedException();
        }
    }
}
