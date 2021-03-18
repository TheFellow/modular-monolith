using Lamar;
using MediatR;
using Xpl.Framework.Messaging.Commands;
using Xpl.Framework.Messaging.Commands.Pipeline;

namespace Xpl.Framework.Messaging.IoC
{
    public class ModuleRegistry : ServiceRegistry
    {
        public ModuleRegistry()
        {
            For<ICommandBus>().Use<CommandBus>().Scoped();
            this.RegisterPipelineFor<ICommandBus, CommandResultLogger>();

            // In reverse order
            // For<ICommandBus>().DecorateAllWith<TDecorator>() where TDecorator : ICommandBus

            For<IMediator>().Use<Mediator>().Scoped();
            For<ServiceFactory>().Use(ctx => ctx.GetInstance);
        }
    }
}
