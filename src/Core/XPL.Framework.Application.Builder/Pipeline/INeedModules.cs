using Lamar;

namespace XPL.Framework.Application.Builder.Pipeline
{
    public interface INeedModules
    {
        INeedModules AddModuleRegistry<TRegistry>() where TRegistry : ServiceRegistry, new();
        IRunnable Build();
    }
}