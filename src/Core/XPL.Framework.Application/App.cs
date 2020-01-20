using Functional.Either;
using Lamar;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Ports;
using XPL.Framework.Application.Ports.Bus;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Application
{
    public abstract class App
    {
        private readonly IContainer _container;
        private IBus Bus => _container.GetNestedContainer().GetInstance<IBus>();

        public AppInfo AppInfo { get; }
        public ILogger Logger { get; }

        public App(AppInfo appInfo, ILogger logger, IContainer container)
        {
            AppInfo = appInfo;
            Logger = logger;
            _container = container;
        }

        public async Task<Either<CommandError, TResult>> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await Bus.ExecuteCommandAsync(command, cancellationToken);
                //uow.Commit()
                return result;
            }
            catch (System.Exception)
            {

                throw;
            }
        }

        public async Task ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) =>
            await Bus.ExecuteQueryAsync(query, cancellationToken);
    }
}
