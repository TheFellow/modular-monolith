using System;

namespace XPL.Framework.Infrastructure.Auditing
{
    public interface IAuditable
    {
        string UpdatedBy { get; set; }
        DateTime UpdatedOn { get; set; }
    }
}
