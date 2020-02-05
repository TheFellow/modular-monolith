using System;

namespace XPL.Framework.Infrastructure.Data
{
    public interface IAuditable
    {
        string UpdatedBy { get; set; }
        DateTime UpdatedOn { get; set; }
    }
}
