using MediatR;

namespace XPL.Framework.Application.Modules.Contracts
{
    public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {

    }
}
