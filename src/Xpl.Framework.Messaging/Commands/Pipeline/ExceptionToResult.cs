using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xpl.Core.Domain;

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
                _logger.Debug("Exception to result: Send");
                return await _bus.Send(command, cancellationToken);
            }
            catch(DomainException dex)
            {
                _logger.Information(dex, "Domain error processing command {Id}", command.CorrelationId);
                return command.Error(dex.Message);

            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing command {Id}", command.CorrelationId);
                return command.Error($"An error occurred processing command {command.CorrelationId}");
            }
            finally
            {
                _logger.Debug("Exception to result: End");
            }
        }
    }
}
