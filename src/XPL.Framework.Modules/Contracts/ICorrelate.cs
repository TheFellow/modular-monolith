using System;

namespace XPL.Framework.Domain.Contracts
{
    public interface ICorrelate
    {
        Guid CorrelationId { get; }
    }
}
