using MediatR;

namespace XPL.Framework.Application.Contracts
{
    public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, Result<TResult>>
        where TCommand : ICommand<TResult>
    {

    }
}
