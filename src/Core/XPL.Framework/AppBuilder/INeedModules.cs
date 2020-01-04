using Lamar;

namespace XPL.Framework.AppBuilder
{
    public interface INeedModules
    {
        INeedModules AddModuleRegistry<TRegistry>() where TRegistry : ServiceRegistry, new();
        App Build();
    }
}