using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Xpl.Framework.Messaging.Commands;

namespace Xpl.Framework.Messaging.MediatR
{
    public class CommandBus : ICommandBus
    {
        private readonly IMediator _mediator;
        public CommandBus(IMediator mediator) => _mediator = mediator;

        public async Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        {
            var request = new CommandWrapper<TResult>(command);
            var commandResult = await _mediator.Send(request, cancellationToken);
            return commandResult.Result;
        }
    }
}
