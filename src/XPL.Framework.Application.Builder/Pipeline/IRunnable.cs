using XPL.Framework.Application.Ports;

namespace XPL.Framework.Application.Builder.Pipeline
{
    public interface IRunnable<TApp>
    {
        TApp Run();
        ILogger Logger { get; }
    }
}