using System.Threading;
using System.Threading.Tasks;

namespace Xpl.Framework.Messaging.Commands
{
    public interface ICommandHandler<in TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken);
    }
}
