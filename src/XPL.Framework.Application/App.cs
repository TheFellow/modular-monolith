using Functional.Either;
using Lamar;
using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Ports;
using XPL.Framework.Application.Ports.Bus;
using XPL.Framework.Kernel;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Application
{
    public abstract class App
    {
        private readonly IContainer _container;

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
                var nested = _container.GetNestedContainer();
                var uowAssembly = command.GetType().Assembly.GetName().Name;
                var uow = nested.GetInstance<IUnitOfWork>(uowAssembly);
                var bus = nested.GetInstance<IBus>();
                
                var result = await bus.ExecuteCommandAsync(command, cancellationToken);
                await uow.CommitAsync();

                return result;
            }
            catch (DomainException ex)
            {
                return new CommandError(ex);
            }
            catch(Exception ex)
            {
                string correlationId = Guid.NewGuid().ToString("N")[..6];
                Logger.Error(ex, "An exception was thrown. Correlation {CorrelationId}", correlationId);
                return new CommandError($"An error occurred and has been logged. Id {correlationId}");
            }
        }

        public async Task<TResult> ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        {
            var nested = _container.GetNestedContainer();
            var bus = nested.GetInstance<IBus>();
            return await bus.ExecuteQueryAsync(query, cancellationToken);
        }
    }
}
