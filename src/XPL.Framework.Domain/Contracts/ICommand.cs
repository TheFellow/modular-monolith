using MediatR;

namespace XPL.Framework.Domain.Contracts
{
    public interface ICommand<TResult> : IRequest<TResult>, ICorrelate
    {
        
    }
}
