using Lamar;
using Microsoft.Extensions.Configuration;
using XPL.Framework.Logging;

namespace XPL.Framework.Application
{
    public abstract class App
    {
        protected readonly IConfiguration _config;
        protected readonly ILogger _logger;
        protected readonly IContainer _container;

        public abstract string ApplicationName { get; }

        public App(IConfiguration config, ILogger logger, IContainer container)
        {
            _config = config;
            _logger = logger;
            _container = container;
        }

        public T GetInstance<T>() => _container.GetInstance<T>();
    }
}
