using System.Threading;
using System.Threading.Tasks;

namespace Xpl.Framework.Messaging.Commands
{
    public interface ICommandDispatcher
    {
        Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken cancellationToken);
    }
}
