using MediatR;

namespace XPL.Framework.Application.Modules.Contracts
{
    public interface IQuery<TResult> : IRequest<TResult> { }
}
