using MediatR;

namespace XPL.Framework.Application.Contracts
{
    public interface ICommand<TResult> : IRequest<TResult>, ICorrelate
    {

    }
}
