using Functional.Option;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Rules;
using XPL.Modules.UserAccess.Infrastructure.Persitence;

namespace XPL.Modules.UserAccess.Infrastructure.UserRegistrations.Rules
{
    public class LoginExists : ILoginExists
    {
        private readonly UserAccessDbContext _dbContext;

        public LoginExists(UserAccessDbContext dbContext) => _dbContext = dbContext;

        bool ILoginExists.LoginExists(Login login)
        {
            var loginSql = _dbContext
                .UserRegistrations.AsNoTracking()
                .Where(u => u.Login == login.Value)
                .Select(l => l.Login)
                .FirstOrNone();

            return loginSql is Some<string>;
        }
    }
}
