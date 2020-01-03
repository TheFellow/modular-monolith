using XPL.Framework.Logging;
using XPL.Framework.Modules.Startup;

namespace XPL.Modules.UserAccess.Startup
{
    public class TestOnStartup : IRunOnStartup
    {
        private readonly ILogger _logger;

        public TestOnStartup(ILogger logger) => _logger = logger;
        public void Execute() => _logger.Info($"Executing {GetType().FullName}");
    }
}
