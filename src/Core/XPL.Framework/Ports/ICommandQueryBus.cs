using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Ports
{
    public interface ICommandQueryBus
    {
        Task ExecuteCommandAsync(ICommand command);
        Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command);
        Task ExecuteQueryAsync<TResult>(IQuery<TResult> query);
    }
}