using Functional.Option;
using System.Security.Claims;
using XPL.Framework.Application.Ports;
using XPL.Modules.Kernel.Passwords;
using XPL.Modules.UserAccess.Infrastructure.Query;

namespace XPL.Modules.UserAccess.Infrastructure.Authentication
{
    public class Authenticate : IAuthentication
    {
        private readonly UserAccessQueryContext _queryContext;
        public Authenticate(UserAccessQueryContext queryContext) => _queryContext = queryContext;

        public Option<ClaimsIdentity> Login(string login, string password)
        {
            var (user, success) = _queryContext.Users
                .FirstOrNone(u => u.Login == login)
                .Map(u => (user: u, pwd: Password.Raw(u.PasswordHash, u.PasswordSalt)))
                .Map(t => (t.user, t.pwd.Verify(password)))
                .Reduce((null!, false));

            if (!success)
                return None.Value;

            var identity = new ClaimsIdentity();

            identity.AddClaim(AuthClaim(ClaimTypes.Name, user.Login));
            identity.AddClaim(AuthClaim(ClaimTypes.GivenName, user.FirstName));
            identity.AddClaim(AuthClaim(ClaimTypes.Surname, user.LastName));
            identity.AddClaim(AuthClaim(ClaimTypes.Email, user.Email));

            return identity;
        }

        private Claim AuthClaim(string claimType, string value) =>
            new Claim(claimType, value, ClaimValueTypes.String, IAuthentication.AuthenticationIssuer);
    }
}
