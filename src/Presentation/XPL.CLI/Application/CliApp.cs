using XPL.Framework.Application;
using XPL.Framework.Application.Builder;
using XPL.Framework.Infrastructure.Bus;
using XPL.Framework.Infrastructure.Configuration;
using XPL.Framework.Infrastructure.Logging;
using XPL.Modules.UserAccess.Startup;

namespace XPL.CLI.Application
{
    public sealed class CliApp
    {
        public static App Build() =>
            ApplicationBuilder.Create(nameof(CliApp))
                .WithConfig(ConfigurationFactory.OptionalAppSettingsJson)
                .WithLogger(LoggerFactory.ConsoleInfoLogger)
                .WithBus<InMemoryBus>()
                .AddModuleRegistry<UserAccessServiceRegistry>()
                .Build();
    }
}
