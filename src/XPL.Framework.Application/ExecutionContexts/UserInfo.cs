using System;
using XPL.Framework.Domain;

namespace XPL.Framework.Application.ExecutionContexts
{
    public class UserInfo : IUserInfo
    {
        public string UserName => Environment.UserName;
        public string UserDomainName => Environment.UserDomainName;
        public string UserFullName => UserDomainName + '\\' + UserName;
    }
}
