using System.Linq;
using System.Security.Claims;
using XPL.Framework.Domain;

namespace XPL.Framework.Infrastructure.ExecutionContexts
{
    public class LoggedInUserInfo : IUserInfo
    {
        public string UserFullName => Identity.Claims.Single(c => c.Type == ClaimTypes.Name).Value;

        public ClaimsIdentity Identity { get; }

        public LoggedInUserInfo(ClaimsIdentity identity) => Identity = identity;
    }
}
