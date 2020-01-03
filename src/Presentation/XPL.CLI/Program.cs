using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using XPL.CLI.Application;
using XPL.Framework.Application;
using XPL.Modules.UserAccess;
using XPL.Modules.UserAccess.Startup;

namespace XPL.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            IConfiguration config = GetConfig();
            Framework.Logging.ILogger logger = SetupLogger(config);

            var app = ApplicationBuilder.Create()
                .WithConfig(config)
                .WithLogger(logger)
                .AddModuleRegistry<UserAccessServiceRegistry>()
                .Build<CliApp>();

            var uam = app.UserAccessModule;

        }

        private static IConfiguration GetConfig() =>
            new ConfigurationBuilder()
                .AddJsonFile(Environment.CurrentDirectory + "appSetting.json", optional: true)
                .Build();

        private static Framework.Logging.ILogger SetupLogger(IConfiguration config) =>
            new Framework.Infrastructure.Logging.Logger(
                new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.Console()
                    .CreateLogger());
    }
}
