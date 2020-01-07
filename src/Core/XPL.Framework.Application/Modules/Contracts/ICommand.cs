using MediatR;

namespace XPL.Framework.Application.Modules.Contracts
{
    public interface ICommand : IRequest { }

    public interface ICommand<TResult> : IRequest<TResult> { }
}
