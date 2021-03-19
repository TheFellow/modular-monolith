using Lamar;
using System.Threading;
using System.Threading.Tasks;

namespace Xpl.Framework.Messaging.Commands
{
    public class CommandBus : ICommandBus
    {
        private readonly IContainer _container;
        public CommandBus(IContainer container) => _container = container;

        public async Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken cancellationToken = default)
        {
            using var nested = _container.GetNestedContainer();
            var dispatcher = nested.GetInstance<ICommandDispatcher>();
            return await dispatcher.Send(command, cancellationToken);
        }
    }
}
