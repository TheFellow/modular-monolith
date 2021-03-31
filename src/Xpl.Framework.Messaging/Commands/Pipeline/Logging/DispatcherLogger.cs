using Serilog;
using System;

namespace Xpl.Framework.Messaging.Commands.Pipeline.Logging
{
    public class DispatcherLogger : IDispatcherLogger
    {
        private readonly ILogger _logger;
        public DispatcherLogger(ILogger logger) => _logger = logger;

        public void Sending(Type commandType, Guid correlationId) =>
            _logger.Information("Sending command {CommandType}#{Id}", commandType, correlationId);
        public void Success(Guid correlationId) =>
            _logger.Information("Send Command Success #{Id}", correlationId);
        public void Error(Guid correlationId, string message) =>
            _logger.Error("Send Command Error #{Id}: {Error}", correlationId, message);
    }
}
