using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;
using XPL.Framework.Ports;

namespace XPL.Framework
{
    public sealed class App : ICommandQueryBus
    {
        private readonly ICommandQueryBus _bus;

        public string ApplicationName { get; }

        public App(string appName, ICommandQueryBus bus)
        {
            ApplicationName = appName;
            _bus = bus;
        }

        public Task ExecuteCommandAsync(ICommand command) => _bus.ExecuteCommandAsync(command);
        public Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command) => _bus.ExecuteCommandAsync(command);
        public Task ExecuteQueryAsync<TResult>(IQuery<TResult> query) => _bus.ExecuteQueryAsync(query);
    }
}
