using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Modules
{
    public interface IModule
    {
        Task ExecuteCommandAsync(ICommand command);
        Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command);
        Task ExecuteQueryAsync<TResult>(IQuery<TResult> query);
    }
}