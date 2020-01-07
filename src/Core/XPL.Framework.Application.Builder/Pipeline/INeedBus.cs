using XPL.Framework.Application.Ports.Bus;

namespace XPL.Framework.Application.Builder.Pipeline
{
    public interface INeedBus
    {
        public INeedModules WithBus<TBus>() where TBus : IBus;
    }
}
