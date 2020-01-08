using MediatR;

namespace XPL.Framework.Modules.Contracts
{
    public interface IQuery<TResult> : IRequest<TResult> { }
}
