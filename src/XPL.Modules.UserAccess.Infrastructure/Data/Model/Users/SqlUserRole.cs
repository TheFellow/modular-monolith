using System;
using XPL.Framework.Infrastructure.Data;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users
{
    public class SqlUserRole : IAuditable
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string Role { get; set; } = string.Empty;
        public DateTime BeginOnUtc { get; set; }
        public DateTime? EndOnUtc { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime UpdatedOn { get; set; }
    }
}
