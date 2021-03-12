using MediatR;

namespace Xpl.Framework.Messaging.Commands
{
    public class Command<TResponse> : IRequest<TResponse> { }
}
