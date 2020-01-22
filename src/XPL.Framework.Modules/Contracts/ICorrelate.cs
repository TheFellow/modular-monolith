using System;

namespace XPL.Framework.Modules.Contracts
{
    public interface ICorrelate
    {
        Guid CorrelationId { get; }
    }
}
