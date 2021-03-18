using System;

namespace Xpl.Framework.Messaging.Commands
{
    public class Command<TResult> : ICommand<TResult>
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
    }
}
