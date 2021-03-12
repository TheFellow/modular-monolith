using Lamar;

namespace Xpl.Framework
{
    public class IoC : ServiceRegistry
    {
        public IoC()
        {
            IncludeRegistry<Messaging.IoC>();
        }
    }
}
