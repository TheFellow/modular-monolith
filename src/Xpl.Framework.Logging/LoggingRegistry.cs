using Lamar;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Xpl.Framework.Logging
{
    public class LoggingRegistry : ServiceRegistry
    {
        public LoggingRegistry()
        {
            For<ILogger>().Use(Logger);
        }

        public Logger Logger { get; } = new LoggerConfiguration()
                .WriteTo.Console(LogEventLevel.Information)
                .CreateLogger();
}
}
