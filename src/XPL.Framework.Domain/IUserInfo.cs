using System.Security.Claims;

namespace XPL.Framework.Domain
{
    public interface IUserInfo
    {
        string UserFullName { get; }

        ClaimsIdentity Identity { get; }
    }
}
