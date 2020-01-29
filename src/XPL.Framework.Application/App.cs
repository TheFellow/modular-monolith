using Lamar;
using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Contracts;
using XPL.Framework.Application.Ports;
using XPL.Framework.Application.Ports.Bus;
using XPL.Modules.Kernel;

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

        public async Task<Result<TResult>> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        {
            try
            {
                using var nested = _container.GetNestedContainer();
                var uowAssembly = command.GetType().Assembly.GetName().Name;
                var uow = nested.GetInstance<IUnitOfWork>(uowAssembly);
                var bus = nested.GetInstance<IBus>();
                
                var result = await bus.ExecuteCommandAsync(command, cancellationToken);
                await result.OnOk(async () => await uow.CommitAsync(command.CorrelationId, cancellationToken));

                return result;
            }
            catch (DomainException ex)
            {
                return command.Fail(ex);
            }
            catch(Exception ex)
            {
                string correlationId = Guid.NewGuid().ToString("N")[..6];
                Logger.Error(ex, "An exception was thrown. Correlation {CorrelationId}", correlationId);
                return command.Fail($"An error occurred and has been logged. Id {correlationId}");
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
