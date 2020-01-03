using MediatR;
using System.Threading.Tasks;
using XPL.Framework.Logging;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Modules
{
    public sealed class Module : IModule
    {
        private readonly IMediator _mediator;
        private readonly ILogger _logger;

        public Module(IMediator mediator, ILogger logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public Task ExecuteCommandAsync(ICommand command)
        {
            _logger.Info($"Executing command {command.GetType().FullName}");
            return _mediator.Send(command);
        }

        public Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command)
        {
            _logger.Info($"Executing command {command.GetType().FullName}");
            return _mediator.Send(command);
        }

        public Task ExecuteQueryAsync<TResult>(IQuery<TResult> query)
        {
            _logger.Info($"Executing query {query.GetType().FullName}");
            return _mediator.Send(query);
        }
    }
}
