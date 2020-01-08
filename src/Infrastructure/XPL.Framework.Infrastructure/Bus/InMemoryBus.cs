using Functional.Either;
using Functional.Option;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Ports.Bus;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Infrastructure.Bus
{
    public sealed class InMemoryBus : IBus
    {
        private readonly IMediator _mediator;
        private readonly ICommandValidator _validator;
        
        public InMemoryBus(IMediator mediator, ICommandValidator validator)
        {
            _mediator = mediator;
            _validator = validator;
        }

        public async Task<Either<CommandError, TResult>> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        {
            var errors = _validator.Validate(command);

            if (errors is Some<CommandErrorList> errorList)
                return errorList.Content;

            return await _mediator.Send(command, cancellationToken);
        }

        public Task ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) => _mediator.Send(query, cancellationToken);
    }
}
