using MediatR;

namespace XPL.Framework.Modules.Contracts
{
    public interface IQueryHandler<TQuery, TResult> : IRequestHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {

    }
}
