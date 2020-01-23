using System;
using XPL.Framework.Infrastructure.Persistence;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users
{
    public class SqlUserLogin : ISqlId
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public DateTime BeginOnUtc { get; set; }
        public DateTime? EndOnUtc { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime UpdatedOn { get; set; }
    }
}
