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
            var logger = _logger.ForContext(command.GetType());
            logger.Information("Sending command {Command}", command);

            var result = await _bus.Send(command, cancellationToken);

            if (result is Ok<T> ok)
            {
                _logger.Information("Command success: Returned {@Result}", ok.Value);
            }
            else if (result is Error<T> error)
            {
                _logger.Information("Command failed: Returned {Error}", error.Message);
            }

            return result;
        }
    }
}
