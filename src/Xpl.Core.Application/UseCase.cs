using System.Threading;
using System.Threading.Tasks;
using Xpl.Framework.Messaging;
using Xpl.Framework.Messaging.Commands;

namespace Xpl.Core.Application
{
    public abstract class UseCase<TCommand, TResult> : ICommandHandler<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        public abstract Task<TResult> Handle(TCommand request, CancellationToken cancellationToken);
    }
}
