using XPL.Framework.Domain;

namespace XPL.Framework.Infrastructure.ExecutionContexts
{
    public class AnonymousUserInfo : IUserInfo
    {
        public string UserName => "Anonymous";
        public string UserDomainName => string.Empty;
        public string UserFullName => "Anonymous";
    }
}
