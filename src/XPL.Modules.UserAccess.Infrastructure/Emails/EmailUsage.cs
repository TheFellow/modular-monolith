using Functional.Option;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using XPL.Modules.Kernel.Email;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Data;

namespace XPL.Modules.UserAccess.Infrastructure.Emails
{
    public class EmailUsage : IEmailUsage
    {
        private readonly IUserAccessQueryContext _queryContext;
        public EmailUsage(IUserAccessQueryContext queryContext) => _queryContext = queryContext;

        public Option<Login> TryGetLoginForEmail(EmailAddress newEmail) =>
            _queryContext.Users
            .Where(u => u.Emails.Select(e => e.Email).Contains(newEmail.Value))
            .Select(su => su.Login)
            .ToList() // Can't Concat over different DbSets
            .Concat(
                _queryContext.Registrations
                .Where(r => r.Email == newEmail.Value)
                .Select(r => r.Email))
            .FirstOrNone()
            .Map(su => new Login(su));
    }
}
