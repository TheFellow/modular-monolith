using Functional.Option;
using System.Linq;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.Registrations.Rules;
using XPL.Modules.UserAccess.Infrastructure.Query;
using XPL.Modules.UserAccess.Infrastructure.Query.Model;

namespace XPL.Modules.UserAccess.Infrastructure.Registrations.Rules
{
    public class LoginExists : ILoginExists
    {
        private readonly UserAccessQueryContext _queryContext;

        public LoginExists(UserAccessQueryContext queryContext) => _queryContext = queryContext;

        bool ILoginExists.LoginExists(Login login)
        {
            var loginSql = _queryContext.Logins
                .Where(l => l.Login == login.Value)
                .FirstOrNone();

            return loginSql is Some<SqlLoginView>;
        }
    }
}
