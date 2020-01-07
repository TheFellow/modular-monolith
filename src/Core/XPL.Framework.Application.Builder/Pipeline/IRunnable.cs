using XPL.Framework.Application.Ports;

namespace XPL.Framework.Application.Builder.Pipeline
{
    public interface IRunnable
    {
        App Run();
        ILogger Logger { get; }
    }
}