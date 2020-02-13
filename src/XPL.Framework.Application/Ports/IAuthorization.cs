using System.Security.Claims;

namespace XPL.Framework.Application.Ports
{
    public interface IAuthorization
    {
        bool Authorize(ClaimsIdentity identity, string authClaim);

        public const string AuthorizationIssuer = "XPL Authorization Authority";
    }
}
