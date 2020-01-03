using Lamar;
using Microsoft.Extensions.Configuration;
using XPL.Framework.Application;
using XPL.Framework.Logging;
using XPL.Modules.UserAccess;

namespace XPL.CLI.Application
{
    public class CliApp : App
    {
        public CliApp(IConfiguration config, ILogger logger, IContainer container)
            : base(config, logger, container)
        {
        }

        public override string ApplicationName => nameof(CliApp);

        public UserAccessModule UserAccessModule => _container.GetInstance<UserAccessModule>();
    }
}
