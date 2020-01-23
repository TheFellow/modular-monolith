using System;

namespace XPL.Modules.Kernel
{
    public static class UserInfo
    {
        public static string UserName = Environment.UserName;
        public static string UserDomainName = Environment.UserDomainName;
        public static string UserFullName = UserDomainName + '\\' + UserName;
    }
}
