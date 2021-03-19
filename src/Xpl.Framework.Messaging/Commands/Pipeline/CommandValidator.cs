using System.Threading;
using System.Threading.Tasks;

namespace Xpl.Framework.Messaging.Commands.Pipeline
{
    public class CommandValidator : ICommandDispatcher
    {
        private readonly ICommandDispatcher _bus;

        public CommandValidator(ICommandDispatcher bus)
        {
            // TODO: Add validators for commands
            _bus = bus;
        }

        public async Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken cancellationToken = default)
        {
            var result = await _bus.Send(command, cancellationToken);
            return result;
        }
    }
}
