using Lamar;
using Xpl.Framework.Messaging.MediatR;

namespace Xpl.Framework
{
    public class XplRegistry : ServiceRegistry
    {
        public XplRegistry()
        {
            IncludeRegistry<MediatRRegistry>();
        }
    }
}
