using MediatR;

namespace Xpl.Framework.Messaging
{
    public interface ICommand<TResult> : IRequest<TResult>
    {
        
    }
}
