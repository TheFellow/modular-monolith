using XPL.Framework.Ports;

namespace XPL.Framework.Application.Builder
{
    public interface INeedLogging
    {
        public INeedCommandQueryBus WithLogger(ILogger logger);
    }
}
