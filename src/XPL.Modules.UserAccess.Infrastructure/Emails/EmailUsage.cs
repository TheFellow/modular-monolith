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
        private readonly IUserAccessQueryContext _dbContext;
        public EmailUsage(IUserAccessQueryContext dbContext) => _dbContext = dbContext;

        public Option<Login> TryGetLoginForEmail(EmailAddress newEmail) =>
            _dbContext.Users
            .Where(u => u.Emails.Select(e => e.Email).Contains(newEmail.Value))
            .Select(su => su.Login)
            .ToList() // Can't Concat over different DbSets
            .Concat(
                _dbContext.UserRegistrations
                .Where(r => r.Email == newEmail.Value)
                .Select(r => r.Email))
            .FirstOrNone()
            .Map(su => new Login(su));
    }
}
