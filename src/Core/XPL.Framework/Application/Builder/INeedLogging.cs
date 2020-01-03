using XPL.Framework.Logging;

namespace XPL.Framework.Application.Builder
{
    public interface INeedLogging
    {
        public INeedModules WithLogger(ILogger logger);
    }
}
