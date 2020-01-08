using Functional.Either;
using MediatR;

namespace XPL.Framework.Modules.Contracts
{
    public interface ICommand<TResult> : IRequest<Either<CommandError, TResult>> { }
}
