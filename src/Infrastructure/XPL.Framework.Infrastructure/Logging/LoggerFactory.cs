using Serilog;

namespace XPL.Framework.Infrastructure.Logging
{
    public static class LoggerFactory
    {
        public static Ports.ILogger ConsoleDebugLogger => new SerilogLoggingAdapter(
            new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger());

        public static Ports.ILogger ConsoleInfoLogger => new SerilogLoggingAdapter(
            new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger());
    }
}
