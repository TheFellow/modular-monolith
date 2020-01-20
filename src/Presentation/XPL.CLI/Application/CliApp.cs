using Lamar;
using XPL.Framework.Application;
using XPL.Framework.Application.Builder;
using XPL.Framework.Application.Builder.Pipeline;
using XPL.Framework.Application.Ports;
using XPL.Framework.Infrastructure.Configuration;
using XPL.Framework.Infrastructure.Logging;
using XPL.Modules.UserAccess.Application.Startup;

namespace XPL.CLI.Application
{
    public sealed class CliApp : App
    {
        private static readonly AppInfo _appInfo = new AppInfo(nameof(CliApp)) { Type = "Command Line" };

        public CliApp(ILogger logger, IContainer container)
            : base(_appInfo, logger, container)
        {
        }

        public static IRunnable<CliApp> Build() =>
            ApplicationBuilder.Create()
                .WithConfig(ConfigurationFactory.OptionalAppSettingsJson)
                .WithLogger(LoggerFactory.ConsoleDebugLogger)
                .WithConnectionString(new CliAppConnectionString())
                .AddModuleRegistry<UserAccessServiceRegistry>()
                .Build<CliApp>();
        
    }
}
