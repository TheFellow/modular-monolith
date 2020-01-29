using MediatR;

namespace XPL.Framework.Application.Contracts
{
    public interface IQueryHandler<TQuery, TResult> : IRequestHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {

    }
}
