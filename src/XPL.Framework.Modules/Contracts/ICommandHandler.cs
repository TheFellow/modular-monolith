using Functional.Either;
using MediatR;

namespace XPL.Framework.Modules.Contracts
{
    public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, Either<CommandError, TResult>>
        where TCommand : ICommand<TResult>
    {

    }
}
