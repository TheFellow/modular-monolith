using Lamar;

namespace XPL.Framework.Application.Builder
{
    public interface INeedModules
    {
        INeedModules AddModuleRegistry<TRegistry>() where TRegistry : ServiceRegistry, new();
        TApp Build<TApp>() where TApp : App;
    }
}