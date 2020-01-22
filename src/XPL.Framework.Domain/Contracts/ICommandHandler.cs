using MediatR;

namespace XPL.Framework.Domain.Contracts
{
    public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {

    }
}
