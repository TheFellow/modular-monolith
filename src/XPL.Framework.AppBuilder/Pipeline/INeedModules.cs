using Lamar;

namespace XPL.Framework.AppBuilder.Pipeline
{
    public interface INeedModules
    {
        INeedModules AddModuleRegistry<TRegistry>() where TRegistry : ServiceRegistry, new();
        IRunnable<TApp> Build<TApp>() where TApp : class;
    }
}