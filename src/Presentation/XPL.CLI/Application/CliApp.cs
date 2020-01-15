using XPL.Framework.Application;
using XPL.Framework.Application.Builder;
using XPL.Framework.Application.Builder.Pipeline;
using XPL.Framework.Infrastructure.Bus;
using XPL.Framework.Infrastructure.Configuration;
using XPL.Framework.Infrastructure.Logging;
using XPL.Modules.UserAccess.Application.Startup;

namespace XPL.CLI.Application
{
    public sealed class CliApp
    {
        public static IRunnable Build() =>
            ApplicationBuilder.Create(AppInfo)
                .WithConfig(ConfigurationFactory.OptionalAppSettingsJson)
                .WithLogger(LoggerFactory.ConsoleInfoLogger)
                .WithConnectionString(new CliAppConnectionString())
                .WithBus<InMemoryBus>()
                .AddModuleRegistry<UserAccessServiceRegistry>()
                .Build();

        private static AppInfo AppInfo => new AppInfo(nameof(CliApp)) { Type = "Command Line" };
    }
}
