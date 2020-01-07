using XPL.Framework.Application.Ports;

namespace XPL.Framework.Application.Builder.Pipeline
{
    public interface INeedLogging
    {
        public INeedBus WithLogger(ILogger logger);
    }
}
