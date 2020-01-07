using XPL.Framework.Ports;

namespace XPL.Framework.Application.Builder
{
    public interface INeedCommandQueryBus
    {
        public INeedModules WithBus<TBus>() where TBus : ICommandQueryBus;
    }
}
