using System.Security.Claims;
using XPL.Framework.Domain;

namespace XPL.Framework.Infrastructure.ExecutionContexts
{
    public class AnonymousUserInfo : IUserInfo
    {
        public string UserFullName => "Anonymous";

        public ClaimsIdentity Identity => new ClaimsIdentity();
    }
}
