using System;

namespace XPL.Framework.Application.Contracts
{
    public interface ICorrelate
    {
        Guid CorrelationId { get; }
    }
}
