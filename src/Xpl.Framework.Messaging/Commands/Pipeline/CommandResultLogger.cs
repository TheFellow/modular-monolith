using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace Xpl.Framework.Messaging.Commands.Pipeline
{
    public class CommandResultLogger : ICommandBus
    {
        private readonly ICommandBus _bus;
        private readonly ILogger _logger;
        public CommandResultLogger(ICommandBus bus, ILogger logger)
        {
            _bus = bus;
            _logger = logger;
        }

        public async Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken cancellationToken = default)
        {            
            _logger.Information("Sending {Command}#{Id}", command, command.CorrelationId);

            var result = await _bus.Send(command, cancellationToken);

            if (result is Ok<T> ok)
            {
                _logger.Information("Success {Command}#{Id}: {@Result}", command, command.CorrelationId, ok.Value);
            }
            else if (result is Error<T> error)
            {
                _logger.Error("Error {Command}#{Id}: {Error}", command, command.CorrelationId, error.Message);
            }

            return result;
        }
    }
}
