using Serilog;
using XPL.Framework.Application;

namespace XPL.CLI.Application
{
    public class CliApp : App
    {
        public override string ApplicationName => nameof(CliApp);

        public override Framework.Logging.ILogger Logger { get; }

        public CliApp()
        {
            var logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            Logger = new Framework.Infrastructure.Logging.Logger(logger);
        }
    }
}
