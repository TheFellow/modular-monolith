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

        public Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command) => _bus.ExecuteCommandAsync(command);
        public Task ExecuteQueryAsync<TResult>(IQuery<TResult> query) => _bus.ExecuteQueryAsync(query);
    }
}
