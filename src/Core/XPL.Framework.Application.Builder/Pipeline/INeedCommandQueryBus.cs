using XPL.Framework.Application.Ports;

namespace XPL.Framework.Application.Builder.Pipeline
{
    public interface INeedCommandQueryBus
    {
        public INeedModules WithBus<TBus>() where TBus : ICommandQueryBus;
    }
}
