using Functional.Option;
using System.Linq;
using System.Security.Claims;
using XPL.Framework.Application.Ports;
using XPL.Modules.Kernel.Passwords;
using XPL.Modules.UserAccess.Infrastructure.Query;
using XPL.Modules.UserAccess.Infrastructure.Query.Model;

namespace XPL.Modules.UserAccess.Infrastructure.Auth
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

            AddAuthenitcationClaims(user, identity);
            AddAuthorizationClaims(identity);

            return identity;
        }


        private void AddAuthenitcationClaims(SqlViewUser user, ClaimsIdentity identity)
        {
            identity.AddClaim(AuthenticationClaim(ClaimTypes.Name, user.Login));
            identity.AddClaim(AuthenticationClaim(ClaimTypes.GivenName, user.FirstName));
            identity.AddClaim(AuthenticationClaim(ClaimTypes.Surname, user.LastName));
            identity.AddClaim(AuthenticationClaim(ClaimTypes.Email, user.Email));
        }

        private void AddAuthorizationClaims(ClaimsIdentity identity)
        {
            var roles = _queryContext.Roles
                .Where(r => r.Login == identity.Name)
                .Select(r => r.Role)
                .ToList();

            foreach (var role in roles)
                identity.AddClaim(new Claim(ClaimTypes.Role, role, ClaimValueTypes.String, IAuthorization.AuthorizationIssuer));
        }

        private Claim AuthenticationClaim(string claimType, string value) =>
            new Claim(claimType, value, ClaimValueTypes.String, IAuthentication.AuthenticationIssuer);
    }
}
