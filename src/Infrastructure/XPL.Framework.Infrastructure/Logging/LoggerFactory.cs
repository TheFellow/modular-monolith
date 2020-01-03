using Serilog;

namespace XPL.Framework.Infrastructure.Logging
{
    public static class LoggerFactory
    {
        public static Framework.Logging.ILogger ConsoleDebugLogger => new Logger(
            new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger());

        public static Framework.Logging.ILogger ConsoleInfoLogger => new Logger(
            new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger());
    }
}
