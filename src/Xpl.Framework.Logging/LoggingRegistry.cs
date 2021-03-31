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
                //.WriteTo.Console(new CompactJsonFormatter(), LogEventLevel.Information)
                .WriteTo.Console(LogEventLevel.Debug,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {EventId}] {Message:lj}{NewLine}{Exception}")
                .Enrich.FromLogContext()
                .CreateLogger();
}
}
