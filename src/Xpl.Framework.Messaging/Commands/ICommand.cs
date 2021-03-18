using MediatR;
using System;

namespace Xpl.Framework.Messaging.Commands
{
    public interface ICommand<TResult> : IRequest<TResult>
    {
        public Guid CorrelationId { get; }
    }
}
