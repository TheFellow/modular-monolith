using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Xpl.Framework.Messaging.Commands
{
    public class CommandDispatcher : ICommandDispatcher

    {
        private readonly IMediator _mediator;
        public CommandDispatcher(IMediator mediator) => _mediator = mediator;

        public async Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken cancellationToken) =>
            await _mediator.Send(command, cancellationToken);
    }
}
