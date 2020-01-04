using XPL.Framework.Ports;

namespace XPL.Framework.AppBuilder
{
    public interface INeedLogging
    {
        public INeedModules WithLogger(ILogger logger);
    }
}
