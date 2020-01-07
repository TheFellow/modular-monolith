using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Application.Ports
{
    public interface ICommandQueryBus
    {
        Task ExecuteCommandAsync(ICommand command);
        Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command);
        Task ExecuteQueryAsync<TResult>(IQuery<TResult> query);
    }
}