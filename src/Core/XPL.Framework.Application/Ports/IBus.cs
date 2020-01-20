using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Application.Ports.Bus
{
    public interface IBus
    {
        Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);

        Task ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
    }
}