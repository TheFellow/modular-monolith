using MediatR;

namespace Xpl.Framework.Messaging.Commands
{
    public interface ICommand<TResult> : IRequest<TResult>
    {

    }
}
