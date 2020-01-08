using Functional.Either;
using MediatR;

namespace XPL.Framework.Application.Modules.Contracts
{
    public interface ICommand<TResult> : IRequest<Either<ICommandError, TResult>> { }
}
