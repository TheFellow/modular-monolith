using Lamar;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Xpl.Framework.Messaging.Commands
{

    /*
        - 
        - Logging of Result
        - Exception to Result
        - 
    */
    public class CommandBus : ICommandBus
    {
        private readonly IContainer _container;

        public CommandBus(IContainer container)
        {
            _container = container;
        }
        public async Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken cancellationToken)
        {
            using var nested = _container.GetNestedContainer();
            var mediator = nested.GetInstance<IMediator>();
            try
            {
                var result = await mediator.Send(command, cancellationToken);
                return Result<T>.Ok(result);
            }
            catch (Exception ex)
            {
                return Result<T>.Error(ex.Message);
            }
        }
    }
}
