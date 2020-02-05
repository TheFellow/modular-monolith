using System;

namespace XPL.Framework.Infrastructure.Data
{
    public static class AuditableExtensions
    {
        public static T Audit<T>(this T auditable, string userName, DateTime now)
            where T : IAuditable
        {
            auditable.UpdatedBy = userName;
            auditable.UpdatedOn = now;
            return auditable;
        }

        public static T Audit<T>(this T auditable, Auditor auditor)
            where T : IAuditable => auditable.Audit(auditor.User, auditor.Now);
    }
}
