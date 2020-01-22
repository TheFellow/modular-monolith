using MediatR;

namespace XPL.Framework.Domain.Contracts
{
    public interface IQuery<TResult> : IRequest<TResult> { }
}
