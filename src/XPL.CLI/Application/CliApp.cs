using Functional.Option;
using Lamar;
using System.Security.Claims;
using XPL.Framework.AppBuilder;
using XPL.Framework.AppBuilder.Pipeline;
using XPL.Framework.Application;
using XPL.Framework.Application.Ports;
using XPL.Framework.Domain;
using XPL.Framework.Infrastructure.Configuration;
using XPL.Framework.Infrastructure.ExecutionContexts;
using XPL.Framework.Infrastructure.Logging;
using XPL.Modules.UserAccess.Application.Startup;

namespace XPL.CLI.Application
{
    public sealed class CliApp : App
    {
        public override IUserInfo CurrentUser { get; protected set; } = new AnonymousUserInfo();

        private static readonly AppInfo _appInfo = new AppInfo(nameof(CliApp)) { Type = "Command Line" };
        private readonly IAuthentication _authentication;

        public CliApp(ILogger logger, IContainer container, IAuthentication authentication)
            : base(_appInfo, logger, container)
        {
            _authentication = authentication;
        }

        public static IRunnable<CliApp> Build() =>
            ApplicationBuilder.Create()
                .WithConfig(ConfigurationFactory.OptionalAppSettingsJson)
                .WithLogger(LoggerFactory.ConsoleDebugLogger)
                .WithConnectionString(c => new CliAppConnectionString(c))
                .AddModuleRegistry<UserAccessServiceRegistry>()
                .Build<CliApp>();
        
        public bool Login(string login, string password)
        {
            var option = _authentication.Login(login, password);

            if (!(option is Some<ClaimsIdentity> some))
                return false;

            CurrentUser = new LoggedInUserInfo(some.Content);

            return true;
        }
    }
}
