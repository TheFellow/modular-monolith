using Functional.Option;
using System.Linq;
using XPL.Modules.Kernel.Email;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Query;

namespace XPL.Modules.UserAccess.Infrastructure.Emails
{
    public class EmailUsage : IEmailUsage
    {
        private readonly UserAccessQueryContext _queryContext;
        public EmailUsage(UserAccessQueryContext queryContext) => _queryContext = queryContext;

        public Option<Login> TryGetLoginForEmail(EmailAddress newEmail) =>
            _queryContext.Logins
            .Where(l => l.Email == newEmail.Value)
            .FirstOrNone()
            .Map(sl => new Login(sl.Login));
    }
}
