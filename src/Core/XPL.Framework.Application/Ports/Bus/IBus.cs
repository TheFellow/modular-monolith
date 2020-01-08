using Functional.Either;
using System.Threading.Tasks;
using XPL.Framework.Application.Modules.Contracts;

namespace XPL.Framework.Application.Ports.Bus
{
    public interface IBus
    {
        Task<Either<ICommandError, TResult>> ExecuteCommandAsync<TResult>(ICommand<TResult> command);
        Task ExecuteQueryAsync<TResult>(IQuery<TResult> query);
    }
}