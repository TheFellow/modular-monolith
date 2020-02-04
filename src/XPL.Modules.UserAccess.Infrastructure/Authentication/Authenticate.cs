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
                .Users.Include(u => u.Passwords).Include(u => u.Emails)
                .FirstOrNone(u => u.Login == login)
                .Map(u => (user: u, password: u.Passwords.Single(p => p.EndOnUtc == null)))
                .Map(t => (t.user, password: Password.Raw(t.password.PasswordHash, t.password.PasswordSalt)))
                .Map(t => (t.user, verified: t.password.Verify(password)))
                .Reduce((null!, false));

            if (!success)
                return None.Value;

            var identity = new ClaimsIdentity();

            identity.AddClaim(AuthClaim(ClaimTypes.Name, login));
            identity.AddClaim(AuthClaim(ClaimTypes.GivenName, user.FirstName));
            identity.AddClaim(AuthClaim(ClaimTypes.Surname, user.LastName));
            identity.AddClaim(AuthClaim(ClaimTypes.Email, user.Emails.Single(e => e.Status == SqlUserEmail.ActiveStatus).Email));

            return identity;
        }

        private Claim AuthClaim(string claimType, string value) =>
            new Claim(claimType, value, ClaimValueTypes.String, IAuthentication.AuthenticationIssuer);
    }
}
