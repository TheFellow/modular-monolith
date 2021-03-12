using Lamar;
using MediatR;

namespace Xpl.Framework.Messaging
{
    public class IoC : ServiceRegistry
    {
        public IoC()
        {
            For<ICommandBus>().Use<CommandBus>().Scoped();
            For<IMediator>().Use<Mediator>().Scoped();
            For<ServiceFactory>().Use(ctx => ctx.GetInstance);
        }
    }
}
