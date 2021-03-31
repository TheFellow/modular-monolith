using Lamar;
using MediatR;
using Xpl.Framework.Logging;
using Xpl.Framework.Messaging.Commands;
using Xpl.Framework.Messaging.Commands.Pipeline;

namespace Xpl.Framework.Messaging.IoC
{
    public class MessagingRegistry : ServiceRegistry
    {
        public MessagingRegistry()
        {
            IncludeRegistry<LoggingRegistry>();

            For<ICommandBus>().Use<CommandBus>().Scoped();

            For<ICommandDispatcher>().Use<CommandDispatcher>();
            this.RegisterPipelineFor<ICommandDispatcher, CommandResultLogger, ExceptionToResult, CommandValidator>();

            For<IMediator>().Use<Mediator>().Scoped();
            For<ServiceFactory>().Use(ctx => ctx.GetInstance);

            For<IDispatcherLogger>().Use<DispatcherLogger>().Scoped();
        }
    }
}
