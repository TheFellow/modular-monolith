using System.Threading;
using System.Threading.Tasks;

namespace Xpl.Framework.Messaging.Commands.Pipeline
{
    public class CommandResultLogger : ICommandDispatcher
    {
        private readonly ICommandDispatcher _bus;
        private readonly IDispatcherLogger _logger;
        public CommandResultLogger(ICommandDispatcher bus, IDispatcherLogger logger)
        {
            _bus = bus;
            _logger = logger;
        }

        public async Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken cancellationToken = default)
        {
            _logger.Sending(command.GetType(), command.CorrelationId);

            var result = await _bus.Send(command, cancellationToken);
            if (result is Ok<T>)
            {
                _logger.Success(command.CorrelationId);
            }
            else if (result is Error<T> error)
            {
                _logger.Error(command.CorrelationId, error.Message);
            }

            return result;
        }
    }
}
