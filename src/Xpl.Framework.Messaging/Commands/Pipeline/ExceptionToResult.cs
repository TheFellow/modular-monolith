using System;
using System.Threading;
using System.Threading.Tasks;

namespace Xpl.Framework.Messaging.Commands.Pipeline
{
    public class ExceptionToResult : ICommandBus
    {
        private readonly ICommandBus _bus;
        public ExceptionToResult(ICommandBus bus) => _bus = bus;

        public async Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _bus.Send(command, cancellationToken);
            }
            catch (Exception ex)
            {
                return Result<T>.Error(ex.Message);
            }
        }
    }
}
