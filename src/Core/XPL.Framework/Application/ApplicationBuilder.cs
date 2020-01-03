using Lamar;
using MediatR;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;
using XPL.Framework.Application.Builder;
using XPL.Framework.Logging;
using XPL.Framework.Modules;
using XPL.Framework.Modules.Startup;

namespace XPL.Framework.Application
{
    public class ApplicationBuilder : INeedConfig, INeedLogging, INeedModules
    {
        private ILogger? _logger;
        private IConfiguration? _config;
        private ServiceRegistry _appRegistry = new ServiceRegistry();
        private IList<Assembly> _assemblies = new List<Assembly>();

        private ApplicationBuilder() { }

        public static INeedConfig Create() => new ApplicationBuilder();

        INeedLogging INeedConfig.WithConfig(IConfiguration config)
        {
            _config = config;
            return this;
        }

        INeedModules INeedLogging.WithLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        INeedModules INeedModules.AddModuleRegistry<TRegistry>()
        {
            _appRegistry.IncludeRegistry<TRegistry>();
            _assemblies.Add(typeof(TRegistry).Assembly);

            return this;
        }

        App INeedModules.Build<TApp>()
        {
            if (_config is null) throw new InvalidOperationException("Cannot build application without initializing configueration");
            if (_logger is null) throw new InvalidOperationException("Cannot build application without initializing a logger");

            _appRegistry.For<ILogger>().Use(_logger);
            _appRegistry.For<IConfiguration>().Use(_config);

            _appRegistry.For<IMediator>().Use<Mediator>().Scoped();
            _appRegistry.For<ServiceFactory>().Use(ctx => ctx.GetInstance);

            ScanModules();

            var container = new Container(_appRegistry);

            _logger.Debug(container.WhatDidIScan());
            _logger.Debug(container.WhatDoIHave());

            return container.GetInstance<TApp>();
        }

        private void ScanModules()
        {
            foreach(var assembly in _assemblies)
            {
                _appRegistry.Scan(scan =>
                {
                    scan.Assembly(assembly);

                    scan.AddAllTypesOf<IRunOnStartup>();
                    scan.AddAllTypesOf<IRunOnInit>();
                });
            }
        }
    }
}
