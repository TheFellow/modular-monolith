using Lamar;

namespace Xpl.Framework.Messaging.MediatR
{
    public class MediatRRegistry : ServiceRegistry
    {
        public MediatRRegistry()
        {
            For<ICommandBus>().Use<CommandBus>().Scoped(); // TODO: Setup a nested container for each command bus
        }
    }
}
