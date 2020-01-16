using XPL.Framework.Application.Ports;

namespace XPL.Framework.Application
{
    public abstract class App
    {
        public AppInfo AppInfo { get; }
        public ILogger Logger { get; }

        public App(AppInfo appInfo, ILogger logger)
        {
            AppInfo = appInfo;
            Logger = logger;
        }
    }
}
