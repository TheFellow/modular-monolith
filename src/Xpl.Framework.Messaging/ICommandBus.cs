using System.Threading;
using System.Threading.Tasks;
using Xpl.Framework.Messaging.Results;

namespace Xpl.Framework.Messaging
{
    public interface ICommandBus
    {
        Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken cancellationToken = default);
    }
}
