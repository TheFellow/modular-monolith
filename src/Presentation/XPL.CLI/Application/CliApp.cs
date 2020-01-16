using Lamar;
using XPL.Framework.Application;
using XPL.Framework.Application.Builder;
using XPL.Framework.Application.Builder.Pipeline;
using XPL.Framework.Application.Ports;
using XPL.Framework.Infrastructure.Bus;
using XPL.Framework.Infrastructure.Configuration;
using XPL.Framework.Infrastructure.Logging;
using XPL.Modules.UserAccess.Application.Startup;
using XPL.Modules.UserAccess.Infrastructure.Persitence;

namespace XPL.CLI.Application
{
    public sealed class CliApp : App
    {
        private static readonly AppInfo _appInfo = new AppInfo(nameof(CliApp)) { Type = "Command Line" };
        private readonly IContainer _container;

        public CliApp(ILogger logger, IContainer container)
            : base(_appInfo, logger)
        {
            _container = container;
        }

        public static IRunnable<CliApp> Build() =>
            ApplicationBuilder.Create()
                .WithConfig(ConfigurationFactory.OptionalAppSettingsJson)
                .WithLogger(LoggerFactory.ConsoleDebugLogger)
                .WithConnectionString(new CliAppConnectionString())
                .WithBus<InMemoryBus>()
                .AddModuleRegistry<UserAccessServiceRegistry>()
                .Build<CliApp>();


        
        public UserAccessUoW GetUserAccessUoW() => _container.GetInstance<UserAccessUoW>();
    }
}
