using MediatR;
using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;
using XPL.Framework.Ports;

namespace XPL.Framework.Infrastructure.Bus
{
    public sealed class CommandQueryBus : ICommandQueryBus
    {
        private readonly IMediator _mediator;

        public CommandQueryBus(IMediator mediator) => _mediator = mediator;

        public Task ExecuteCommandAsync(ICommand command) => _mediator.Send(command);

        public Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command) => _mediator.Send(command);

        public Task ExecuteQueryAsync<TResult>(IQuery<TResult> query) => _mediator.Send(query);
    }
}
