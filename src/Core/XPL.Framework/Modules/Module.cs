using MediatR;
using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Modules
{
    public abstract class Module
    {
        private readonly IMediator _mediator;

        public Module(IMediator mediator) => _mediator = mediator;

        public Task ExecuteCommandAsync(ICommand command) => _mediator.Send(command);
        public Task ExecuteCommandAsync<TResult>(ICommand<TResult> command) => _mediator.Send(command);
        public Task ExecuteQueryAsync<TResult>(IQuery<TResult> query) => _mediator.Send(query);
    }
}
