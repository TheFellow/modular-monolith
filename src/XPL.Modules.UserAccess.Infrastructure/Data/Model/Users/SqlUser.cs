﻿using System;
using System.Collections.Generic;
using XPL.Framework.Infrastructure.Persistence;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users
{
    public class SqlUser : ISqlId
    {
        public long Id { get; set; }
        public byte[] RowVersion { get; set; } = new byte[0];
        public Guid UserId { get; set; } = Guid.Empty;
        public Guid? RegistrationId { get; set; }
        public string Login { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime UpdatedOn { get; set; }

        public List<SqlUserEmail>? Emails { get; set; }
        public List<SqlUserLogin>? Logins { get; set; }
    }
}