using Lamar;
using MediatR;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;
using XPL.Framework.Application.Builder.Pipeline;
using XPL.Framework.Application.Modules.Contracts;
using XPL.Framework.Application.Ports;
using XPL.Framework.Application.Ports.Bus;
using XPL.Framework.Infrastructure.Bus.Validation;
using XPL.Framework.Modules.Startup;

namespace XPL.Framework.Application.Builder
{
    public class ApplicationBuilder : INeedConfig, INeedLogging, INeedModules, INeedBus
    {
        private ILogger? _logger;
        private IConfiguration? _config;
        private Type? _busType;
        private readonly AppInfo _appInfo;
        private readonly ServiceRegistry _appRegistry = new ServiceRegistry();
        private readonly IList<Assembly> _assemblies = new List<Assembly>();

        private ApplicationBuilder(string appName) : this(new AppInfo(appName)) { }
        private ApplicationBuilder(AppInfo appInfo) => _appInfo = appInfo;

        public static INeedConfig Create(string appName) => new ApplicationBuilder(appName);
        public static INeedConfig Create(AppInfo appInfo) => new ApplicationBuilder(appInfo);

        INeedLogging INeedConfig.WithConfig(IConfiguration config)
        {
            _config = config;
            return this;
        }

        INeedBus INeedLogging.WithLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        INeedModules INeedBus.WithBus<TBus>()
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

        IRunnable INeedModules.Build()
        {
            if (_config is null) throw new InvalidOperationException("Cannot build application without initializing configuration");
            if (_logger is null) throw new InvalidOperationException("Cannot build application without initializing a logger");
            if (_busType is null) throw new InvalidOperationException("Cannot build application without initializing a Command/Query bus");

            BootstrapApp(_config, _logger);

            var container = new Container(_appRegistry);

            _logger.Debug(container.WhatDidIScan());
            _logger.Debug(container.WhatDoIHave());

            var app = new App(_appInfo, container.GetInstance<IBus>(), _logger);
            return new Runner(app, _logger, container);
        }

        private void BootstrapApp(IConfiguration config, ILogger logger)
        {
            AddConfig(config);
            AddLogging(logger);
            AddMediator();
            AddCommandQueryBus();
            AddModuleContracts();
            AddStartupClasses();
        }

        #region Bootstrap Application

        private void AddLogging(ILogger logger) => _appRegistry.For<ILogger>().Use(logger);
        private void AddConfig(IConfiguration config) => _appRegistry.For<IConfiguration>().Use(config);
        private void AddMediator()
        {
            _appRegistry.For<IMediator>().Use<Mediator>().Transient();
            _appRegistry.For<ServiceFactory>().Use(ctx => ctx.GetInstance);
        }
        private void AddCommandQueryBus() => _appRegistry.For<IBus>().Use(ctx => (IBus)ctx.GetInstance(_busType));
        private void AddModuleContracts()
        {
            _appRegistry.For<ICommandValidator>().Use<CommandValidator>().Transient();

            foreach (var assembly in _assemblies)
            {
                _appRegistry.Scan(scan =>
                {
                    scan.Assembly(assembly);

                    scan.ConnectImplementationsToTypesClosing(typeof(ICommand<>));
                    scan.ConnectImplementationsToTypesClosing(typeof(ICommandHandler<,>));
                    scan.ConnectImplementationsToTypesClosing(typeof(ICommandRule<>));

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

                    scan.AddAllTypesOf<IRunOnStartup>();
                    scan.AddAllTypesOf<IRunOnInit>();
                });
            }
        }

        #endregion

        private class Runner : IRunnable
        {
            private readonly App _app;
            private readonly IContainer _container;

            public ILogger Logger { get; }

            public Runner(App app, ILogger logger, IContainer container)
            {
                _app = app;
                Logger = logger;
                _container = container;
            }

            public App Run()
            {
                Logger.Info("Starting {@Application}", _app.AppInfo);

                RunOnStartup();
                RunOnInit();

                return _app;
            }

            private void RunOnStartup()
            {
                var onStartups = _container.GetAllInstances<IRunOnStartup>();
                foreach (var onStartup in onStartups)
                {
                    try
                    {
                        onStartup.Execute();
                    }
                    catch (Exception ex)
                    {
                        Logger?.Fatal(ex, "An error occurred OnStartup for {OnStartup}", onStartup.GetType().FullName);
                        throw;
                    }
                }
            }
            private void RunOnInit()
            {
                var onInits = _container.GetAllInstances<IRunOnInit>();
                foreach (var onInit in onInits)
                {
                    try
                    {
                        onInit.Execute();
                    }
                    catch (Exception ex)
                    {
                        Logger?.Fatal(ex, "An error occurred OnInit for {OnInit}", onInit.GetType().FullName);
                        throw;
                    }
                }
            }
        }
    }
}
