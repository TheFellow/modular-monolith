using System;
using XPL.Framework.Domain;

namespace XPL.Framework.Infrastructure.Auditing
{
    public sealed class Auditor
    {
        public DateTime Now { get; }
        public DateTime UtcNow { get; }
        public string User { get; }

        public Auditor(IExecutionContext executionContext)
        {
            Now = executionContext.SystemClock.Now;
            UtcNow = executionContext.SystemClock.UtcNow;
            User = executionContext.UserInfo.UserFullName;
        }
    }
}
