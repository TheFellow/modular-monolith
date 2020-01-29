using MediatR;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Contracts;
using XPL.Framework.Application.Ports;
using XPL.Framework.Application.Ports.Bus;

namespace XPL.Framework.Infrastructure.Bus
{
    public sealed class InMemoryBus : IBus
    {
        private readonly IMediator _mediator;
        private readonly ILogger _logger;

        public InMemoryBus(IMediator mediator, ILogger logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) =>
            await _mediator.Send(command, cancellationToken);

        public Task<TResult> ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) =>
            _mediator.Send(query, cancellationToken);
    }
}
