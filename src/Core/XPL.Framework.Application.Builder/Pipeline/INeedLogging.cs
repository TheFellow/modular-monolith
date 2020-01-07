using XPL.Framework.Application.Ports;

namespace XPL.Framework.Application.Builder.Pipeline
{
    public interface INeedLogging
    {
        public INeedCommandQueryBus WithLogger(ILogger logger);
    }
}
