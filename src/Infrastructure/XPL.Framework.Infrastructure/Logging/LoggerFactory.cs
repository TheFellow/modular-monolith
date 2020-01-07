using Serilog;

namespace XPL.Framework.Infrastructure.Logging
{
    public static class LoggerFactory
    {
        public static Application.Ports.ILogger ConsoleDebugLogger => new SerilogLoggingAdapter(
            new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger());

        public static Application.Ports.ILogger ConsoleInfoLogger => new SerilogLoggingAdapter(
            new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger());
    }
}
