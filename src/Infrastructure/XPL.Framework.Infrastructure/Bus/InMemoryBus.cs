using MediatR;
using System.Threading.Tasks;
using XPL.Framework.Application.Ports;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Infrastructure.Bus
{
    public sealed class InMemoryBus : ICommandQueryBus
    {
        private readonly IMediator _mediator;

        public InMemoryBus(IMediator mediator) => _mediator = mediator;

        public Task ExecuteCommandAsync(ICommand command) => _mediator.Send(command);

        public Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command) => _mediator.Send(command);

        public Task ExecuteQueryAsync<TResult>(IQuery<TResult> query) => _mediator.Send(query);
    }
}
