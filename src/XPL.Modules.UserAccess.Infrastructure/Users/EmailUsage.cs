using Functional.Option;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using XPL.Modules.Kernel.Email;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Data;

namespace XPL.Modules.UserAccess.Infrastructure.Users
{
    public class EmailUsage : IEmailUsage
    {
        private readonly UserAccessDbContext _dbContext;
        public EmailUsage(UserAccessDbContext dbContext) => _dbContext = dbContext;

        // TODO: Update this to also check User table
        public Option<Login> TryGetLoginForEmail(EmailAddress newEmail) => _dbContext.Users.AsNoTracking()
            .Where(u => u.Emails.Select(e => e.Email).Contains(newEmail.Value))
            .FirstOrNone()
            .Map(su => new Login(su.Login));
    }
}
