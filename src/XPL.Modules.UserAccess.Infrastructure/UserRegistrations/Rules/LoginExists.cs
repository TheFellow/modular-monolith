using Functional.Option;
using System.Linq;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Rules;
using XPL.Modules.UserAccess.Infrastructure.Data;

namespace XPL.Modules.UserAccess.Infrastructure.UserRegistrations.Rules
{
    public class LoginExists : ILoginExists
    {
        private readonly IUserAccessQueryContext _queryContext;

        public LoginExists(IUserAccessQueryContext queryContext) => _queryContext = queryContext;

        bool ILoginExists.LoginExists(Login login)
        {
            var loginSql = _queryContext
                .UserRegistrations
                .Where(u => u.Login == login.Value)
                .Select(l => l.Login)
                .ToList() // Can't Concat over different DbSets
                .Concat(
                    _queryContext.Users
                    .Where(u => u.Login == login.Value)
                    .Select(l => l.Login))
                .FirstOrNone();

            return loginSql is Some<string>;
        }
    }
}
