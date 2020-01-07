using System.Threading.Tasks;
using XPL.Framework.Application.Modules.Contracts;
using XPL.Framework.Application.Ports;

namespace XPL.Framework.Application
{
    public sealed partial class App : ICommandQueryBus
    {
        private readonly ICommandQueryBus _bus;

        public string ApplicationName { get; }
        public ILogger Logger { get; }

        public App(string appName, ICommandQueryBus bus, ILogger logger)
        {
            ApplicationName = appName;
            _bus = bus;
            Logger = logger;
        }

        public Task ExecuteCommandAsync(ICommand command) => _bus.ExecuteCommandAsync(command);
        public Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command) => _bus.ExecuteCommandAsync(command);
        public Task ExecuteQueryAsync<TResult>(IQuery<TResult> query) => _bus.ExecuteQueryAsync(query);
    }
}
