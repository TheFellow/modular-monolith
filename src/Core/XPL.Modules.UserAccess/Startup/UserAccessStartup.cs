using Lamar;
using XPL.Framework.Modules.Startup;

namespace XPL.Modules.UserAccess.Startup
{
    public class UserAccessStartup : ModuleStartup
    {
        public override string ModuleName => nameof(UserAccess);

        public override ServiceRegistry ModuleRegistry => new UserAccessServiceRegistry();
    }
}
