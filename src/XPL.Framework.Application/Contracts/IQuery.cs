using MediatR;

namespace XPL.Framework.Application.Contracts
{
    public interface IQuery<TResult> : IRequest<TResult> { }
}
