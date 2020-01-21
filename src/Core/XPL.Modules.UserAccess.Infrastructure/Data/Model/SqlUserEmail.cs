using System;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model
{
    public class SqlUserEmail
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StatusDate { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime UpdatedOn { get; set; }
    }
}
