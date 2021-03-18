using Lamar;
using Xpl.Framework.Logging;
using Xpl.Framework.Messaging.IoC;

namespace Xpl.Framework
{
    public class IoC : ServiceRegistry
    {
        public IoC()
        {
            IncludeRegistry<MessagingRegistry>();
            IncludeRegistry<LoggingRegistry>();
        }
    }
}
