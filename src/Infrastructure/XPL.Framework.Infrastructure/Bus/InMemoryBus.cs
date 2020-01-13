using Functional.Either;
using Functional.Option;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Ports;
using XPL.Framework.Application.Ports.Bus;
using XPL.Framework.Domain;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Infrastructure.Bus
{
    public sealed class InMemoryBus : IBus
    {
        private readonly IMediator _mediator;
        private readonly ICommandValidator _validator;
        private readonly ILogger _logger;

        public InMemoryBus(IMediator mediator, ICommandValidator validator, ILogger logger)
        {
            _mediator = mediator;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Either<CommandError, TResult>> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        {
            try
            {
                var errors = _validator.Validate(command);

                if (errors is Some<CommandErrorList> errorList)
                    return errorList.Content;

                return await _mediator.Send(command, cancellationToken);
            }
            catch (DomainException ex)
            {
                return new CommandError(ex);
            }
            catch (Exception ex)
            {
                var guid = Guid.NewGuid();
                string shortId = guid.ToString("N")[..6];
                _logger.Error(ex, "An unhandled error occurred in the Bus. {CorrelationId}", guid);
                return new CommandError($"An error has been logged. Correlation Id {shortId}.") { CorrelationId = guid };
            }
        }

        public Task ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) => _mediator.Send(query, cancellationToken);
    }
}
