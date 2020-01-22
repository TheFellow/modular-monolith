using MediatR;
using System;

namespace XPL.Framework.Modules.Domain
{
    public interface IDomainEvent : INotification
    {
        public Guid CorrelationId { get; }
    }
}
