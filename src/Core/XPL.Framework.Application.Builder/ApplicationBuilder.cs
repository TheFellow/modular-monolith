using Lamar;
using MediatR;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;
using XPL.Framework.Modules.Contracts;
using XPL.Framework.Modules.Startup;
using XPL.Framework.Ports;

namespace XPL.Framework.Application.Builder
{
    public class ApplicationBuilder : INeedConfig, INeedLogging, INeedModules, INeedCommandQueryBus
    {
        private ILogger? _logger;
        private IConfiguration? _config;
        private Type? _busType;
        private readonly string _appName;
        private readonly ServiceRegistry _appRegistry = new ServiceRegistry();
        private readonly IList<Assembly> _assemblies = new List<Assembly>();

        private ApplicationBuilder(string appName) => _appName = appName;

        public static INeedConfig Create(string appName) => new ApplicationBuilder(appName);

        INeedLogging INeedConfig.WithConfig(IConfiguration config)
        {
            _config = config;
            return this;
        }

        INeedCommandQueryBus INeedLogging.WithLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        INeedModules INeedCommandQueryBus.WithBus<TBus>()
        {
            _busType = typeof(TBus);
            return this;
        }

        INeedModules INeedModules.AddModuleRegistry<TRegistry>()
        {
            _appRegistry.IncludeRegistry<TRegistry>();
            _assemblies.Add(typeof(TRegistry).Assembly);

            return this;
        }

        App INeedModules.Build()
        {
            if (_config is null) throw new InvalidOperationException("Cannot build application without initializing configuration");
            if (_logger is null) throw new InvalidOperationException("Cannot build application without initializing a logger");
            if (_busType is null) throw new InvalidOperationException("Cannot build application without initializing a Command/Query bus");

            BootstrapAppContainer(_config, _logger);

            var container = new Container(_appRegistry);

            _logger.Debug(container.WhatDidIScan());
            _logger.Debug(container.WhatDoIHave());

            RunOnStartup(container);
            RunOnInit(container);

            return new App(_appName, container.GetInstance<ICommandQueryBus>());
        }

        private void BootstrapAppContainer(IConfiguration config, ILogger logger)
        {
            AddConfig(config);
            AddLogging(logger);
            AddMediator();
            AddCommandQueryBus();
            AddCommandQueryHandlers();
            AddStartupClasses();
        }

        private void AddCommandQueryBus() => _appRegistry.For<ICommandQueryBus>().Use(ctx => (ICommandQueryBus)ctx.GetInstance(_busType));
        private void AddLogging(ILogger logger) => _appRegistry.For<ILogger>().Use(logger);
        private void AddConfig(IConfiguration config) => _appRegistry.For<IConfiguration>().Use(config);
        private void AddMediator()
        {
            _appRegistry.For<IMediator>().Use<Mediator>().Transient();
            _appRegistry.For<ServiceFactory>().Use(ctx => ctx.GetInstance);
        }
        private void AddCommandQueryHandlers()
        {
            foreach (var assembly in _assemblies)
            {
                _appRegistry.Scan(scan =>
                {
                    scan.Assembly(assembly);

                    scan.AddAllTypesOf<ICommand>();
                    scan.ConnectImplementationsToTypesClosing(typeof(ICommand<>));
                    scan.ConnectImplementationsToTypesClosing(typeof(ICommandHandler<>));
                    scan.ConnectImplementationsToTypesClosing(typeof(ICommandHandler<,>));
                    scan.ConnectImplementationsToTypesClosing(typeof(IQuery<>));
                    scan.ConnectImplementationsToTypesClosing(typeof(IQueryHandler<,>));
                });
            }
        }
        private void AddStartupClasses()
        {
            foreach (var assembly in _assemblies)
            {
                _appRegistry.Scan(scan =>
                {
                    scan.Assembly(assembly);

                    scan.AddAllTypesOf<ICommandQueryBus>();
                    scan.AddAllTypesOf<IRunOnStartup>();
                    scan.AddAllTypesOf<IRunOnInit>();
                });
            }
        }
        private void RunOnStartup(IContainer container)
        {
            var onStartups = container.GetAllInstances<IRunOnStartup>();
            foreach (var onStartup in onStartups)
            {
                try
                {
                    onStartup.Execute();
                }
                catch (Exception ex)
                {
                    _logger?.Fatal(ex, "An error occurred OnStartup");
                    throw;
                }
            }
        }
        private void RunOnInit(IContainer container)
        {
            var onInits = container.GetAllInstances<IRunOnInit>();
            foreach (var onInit in onInits)
            {
                try
                {
                    onInit.Execute();
                }
                catch (Exception ex)
                {
                    _logger?.Fatal(ex, "An error occurred OnInit");
                    throw;
                }
            }
        }
    }
}
