using MediatR;
using Xpl.Framework.Messaging.Commands;

namespace Xpl.Framework.Messaging
{
    public interface ICommandHandler<TCommand, TResult> : IRequestHandler<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        
    }
}
