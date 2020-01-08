using Functional.Either;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Modules.Contracts;
using XPL.Framework.Application.Ports;
using XPL.Framework.Application.Ports.Bus;

namespace XPL.Framework.Application
{
    public sealed partial class App : IBus
    {
        private readonly IBus _bus;

        public AppInfo AppInfo { get; }
        public ILogger Logger { get; }

        public App(AppInfo appInfo, IBus bus, ILogger logger)
        {
            AppInfo = appInfo;
            _bus = bus;
            Logger = logger;
        }

        public Task<Either<CommandError, TResult>> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) => _bus.ExecuteCommandAsync(command, cancellationToken);
        public Task ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) => _bus.ExecuteQueryAsync(query, cancellationToken);
    }
}
