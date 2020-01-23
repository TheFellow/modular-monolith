using XPL.Framework.Application.Ports;

namespace XPL.Framework.AppBuilder.Pipeline
{
    public interface IRunnable<TApp>
    {
        TApp Run();
        ILogger Logger { get; }
    }
}