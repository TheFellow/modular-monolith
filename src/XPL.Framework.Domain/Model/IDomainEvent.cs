using MediatR;
using System;

namespace XPL.Framework.Domain.Model
{
    public interface IDomainEvent : INotification
    {
        public Guid CorrelationId { get; }
    }
}
