using System;
using XPL.Framework.Infrastructure.Persistence;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model
{
    public class SqlUser : ISqlId
    {
        public long Id { get; set; }
        public byte[] RowVersion { get; set; } = new byte[0];
        public Guid UserId { get; set; } = Guid.Empty;
        public string Login { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime UpdatedOn { get; set; }
    }
}
