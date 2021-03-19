using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Xpl.Framework.Messaging.Commands.Pipeline
{
    public class ExceptionToResult : ICommandDispatcher
    {
        private readonly ICommandDispatcher _bus;
        public readonly ILogger _logger;

        public ExceptionToResult(ICommandDispatcher bus, ILogger logger)
        {
            _bus = bus;
            _logger = logger;
        }

        public async Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Information("Exception to result: Send");
                return await _bus.Send(command, cancellationToken);
            }
            catch (Exception ex)
            {
                return Result<T>.Error(ex.Message);
            }
            finally
            {
                _logger.Information("Exception to result: End");
            }
        }
    }
}
