using Lamar;

namespace XPL.Framework.Modules.Startup
{
    public abstract class ModuleStartup
    {
        public abstract string ModuleName { get; }
        public abstract ServiceRegistry ModuleRegistry { get; }
    }
}
