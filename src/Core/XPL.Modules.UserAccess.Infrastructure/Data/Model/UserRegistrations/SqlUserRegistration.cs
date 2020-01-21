using System;
using XPL.Framework.Infrastructure.Persistence;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.UserRegistrations
{
    public class SqlUserRegistration : ISqlId
    {
        public long Id { get; set; }
        public byte[] RowVersion { get; set; } = new byte[0];
        public Guid RegistrationId { get; set; } = Guid.Empty;
        public string Email { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string ConfirmationCode { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StatusDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime UpdatedOn { get; set; }
    }
}
