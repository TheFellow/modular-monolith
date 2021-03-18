using Lamar;
using MediatR;
using Xpl.Framework.Messaging.Commands;

namespace Xpl.Framework.Messaging.IoC
{
    public class ModuleRegistry : ServiceRegistry
    {
        public ModuleRegistry()
        {
            For<ICommandBus>().Use<CommandBus>().Scoped();

            // In reverse order
            // For<ICommandBus>().DecorateAllWith<TDecorator>() where TDecorator : ICommandBus

            For<IMediator>().Use<Mediator>().Scoped();
            For<ServiceFactory>().Use(ctx => ctx.GetInstance);
        }
    }
}
