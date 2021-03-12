using MediatR;
using Xpl.Framework.Messaging.Commands;

namespace Xpl.Framework.Messaging.MediatR
{
    public class CommandWrapper<T> : IRequest<CommandResult<T>>
    {
        public ICommand<T> Command { get; }
        public CommandWrapper(ICommand<T> command) => Command = command;

    }
}
