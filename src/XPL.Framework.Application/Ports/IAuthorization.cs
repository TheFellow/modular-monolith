using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace XPL.Framework.Application.Ports
{
    public interface IAuthorization
    {
        bool Authorize(ClaimsIdentity identity, string authClaim);

        public const string AuthorizationIssuer = "XPL Authorization Authority";
    }
}
