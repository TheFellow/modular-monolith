using XPL.Framework.Application.Ports;

namespace XPL.Framework.AppBuilder.Pipeline
{
    public interface INeedLogging
    {
        public INeedConnectionString WithLogger(ILogger logger);
    }
}
