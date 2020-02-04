using Functional.Option;
using System.Security.Claims;

namespace XPL.Framework.Application.Ports
{
    public interface IAuthentication
    {
        Option<ClaimsIdentity> Login(string login, string password);

        public const string AuthenticationIssuer = "XPL Authentication Authority";
    }
}
