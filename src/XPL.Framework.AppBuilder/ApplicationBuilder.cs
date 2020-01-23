using Lamar;
using MediatR;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;
using XPL.Framework.Application.Ports;
using XPL.Framework.Application.Ports.Bus;
using XPL.Framework.Infrastructure.Bus;
using XPL.Framework.Infrastructure.DomainEvents;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Framework.Kernel.DateTimes;
using XPL.Framework.Domain;
using XPL.Framework.Domain.Contracts;
using XPL.Framework.Domain.Model;
using XPL.Framework.Domain.Startup;
using XPL.Framework.AppBuilder.Pipeline;

namespace XPL.Framework.AppBuilder
{
    public class ApplicationBuilder : INeedConfig, INeedLogging, INeedConnectionString, INeedModules
    {
        private ILogger? _logger;
        private IConfiguration? _config;
        private ConnectionString? _connectionString;
        private readonly ServiceRegistry _appRegistry = new ServiceRegistry();
        private readonly IList<Assembly> _assemblies = new List<Assembly>();

        private ApplicationBuilder() { }

        #region Builder Methods
        public static INeedConfig Create() => new ApplicationBuilder();

        INeedLogging INeedConfig.WithConfig(IConfiguration config)
        {
            _config = config;
            return this;
        }

        INeedConnectionString INeedLogging.WithLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        INeedModules INeedConnectionString.WithConnectionString(ConnectionString connectionString)
        {
            _connectionString = connectionString;
            _appRegistry.For<ConnectionString>().Use(_connectionString);
            return this;
        }

        INeedModules INeedModules.AddModuleRegistry<TRegistry>()
        {
            _appRegistry.IncludeRegistry<TRegistry>();
            _assemblies.Add(typeof(TRegistry).Assembly);

            return this;
        }
        #endregion

        IRunnable<TApp> INeedModules.Build<TApp>()
        {
            if (_config is null) throw new InvalidOperationException("Cannot build application without initializing configuration");
            if (_logger is null) throw new InvalidOperationException("Cannot build application without initializing a logger");
            if (_connectionString is null) throw new InvalidOperationException("Cannot build application without initializing a Connection string");

            BootstrapApp(_config, _logger);

            var container = new Container(_appRegistry);

            _logger.Debug(container.WhatDidIScan());
            _logger.Debug(container.WhatDoIHave());

            var app = container.GetInstance<TApp>();
            return new Runner<TApp>(app, _logger, container);
        }

        private void BootstrapApp(IConfiguration config, ILogger logger)
        {
            AddConfig(config);
            AddLogging(logger);
            AddSystemClock();
            AddMediator();
            AddCommandQueryBus();
            AddModuleContracts();
            AddStartupClasses();
        }

        #region Bootstrap Application

        private void AddConfig(IConfiguration config) => _appRegistry.For<IConfiguration>().Use(config).Singleton();
        private void AddLogging(ILogger logger) => _appRegistry.For<ILogger>().Use(logger).Singleton();
        private void AddSystemClock() => _appRegistry.For<ISystemClock>().Use<SystemClock>();
        private void AddMediator()
        {
            _appRegistry.For<IMediator>().Use<Mediator>();
            _appRegistry.For<ServiceFactory>().Use(ctx => ctx.GetInstance);
        }
        private void AddCommandQueryBus() => _appRegistry.For<IBus>().Use<InMemoryBus>().Scoped();
        private void AddModuleContracts()
        {
            _appRegistry.For<IDomainEventDispatcher>().Use<DomainEventDispatcher>();

            foreach (var assembly in _assemblies)
            {
                _appRegistry.Scan(scan =>
                {
                    scan.Assembly(assembly);

                    scan.ConnectImplementationsToTypesClosing(typeof(ICommand<>));
                    scan.ConnectImplementationsToTypesClosing(typeof(ICommandHandler<,>));

                    scan.ConnectImplementationsToTypesClosing(typeof(IDomainEventHandler<>));

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

        private class Runner<TApp> : IRunnable<TApp>
        {
            private readonly TApp _app;
            private readonly IContainer _container;

            public ILogger Logger { get; }

            public Runner(TApp app, ILogger logger, IContainer container)
            {
                _app = app;
                Logger = logger;
                _container = container;
            }

            public TApp Run()
            {
                Logger.Info("Starting Application");

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
