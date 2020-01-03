using Lamar;
using System;
using System.Linq;
using XPL.Framework.Logging;
using XPL.Framework.Modules.Startup;

namespace XPL.Framework.Application
{
    public abstract class App
    {
        public abstract string ApplicationName { get; }
        public abstract ILogger Logger { get; }

        // This needs to be created after all modules are registered
        private IContainer _container = null!;

        private readonly ServiceRegistry _appRegistry = new ServiceRegistry();

        public void AddModule<TRegistry>() where TRegistry : ServiceRegistry, new()
        {
            _appRegistry.IncludeRegistry<TRegistry>();
        }

        public void Start()
        {
            Logger.Info("Application Start: {ApplicationName}", ApplicationName);

            _container = new Container(_appRegistry);

            try
            {
                RunOnStartup();
                RunOnInit();
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "An error occurred starting {Application}", ApplicationName);
                throw;
            }
        }

        private void RunOnStartup() => _container.GetAllInstances<IRunOnStartup>().ToList().ForEach(s => s.Execute());

        private void RunOnInit() => _container.GetAllInstances<IRunOnInit>().ToList().ForEach(i => i.Execute());
    }
}
