using Functional.Option;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using XPL.Framework.Application.Ports;
using XPL.Modules.Kernel.Passwords;
using XPL.Modules.UserAccess.Infrastructure.Data;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;

namespace XPL.Modules.UserAccess.Infrastructure.Authentication
{
    public class Authenticate : IAuthentication
    {
        private readonly IUserAccessQueryContext _queryContext;
        public Authenticate(IUserAccessQueryContext queryContext) => _queryContext = queryContext;

        public Option<ClaimsIdentity> Login(string login, string password)
        {
            var (user, success) = _queryContext
                .Users.Include(u => u.Passwords)
                .FirstOrNone(u => u.Login == login)
                .Map(u => (user: u, password: u.Passwords.Single(p => p.EndOnUtc == null)))
                .Map(t => (t.user, password: Password.Raw(t.password.PasswordHash, t.password.PasswordSalt)))
                .Map(t => (t.user, verified: t.password.Verify(password)))
                .Reduce((new SqlUser(), false));

            if (!success)
                return None.Value;

            var identity = new ClaimsIdentity();

            identity.AddClaim(new Claim(ClaimTypes.Name, login));
            //identity.AddClaim(new Claim(ClaimTypes.Email, user.Curr))

            return identity;
        }
    }
}
