using Lamar;
using Xpl.Framework.Messaging.IoC;

namespace Xpl.Framework
{
    public class IoC : ServiceRegistry
    {
        public IoC()
        {
            IncludeRegistry<ModuleRegistry>();
        }
    }
}
