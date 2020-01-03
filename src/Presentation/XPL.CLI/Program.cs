using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Threading.Tasks;
using XPL.CLI.Application;
using XPL.Framework.Application;
using XPL.Modules.UserAccess.Startup;
using XPL.Modules.UserAccess.Users.CreateUser;

namespace XPL.CLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            IConfiguration config = GetConfig();
            Framework.Logging.ILogger logger = SetupLogger(config);

            var app = ApplicationBuilder.Create()
                .WithConfig(config)
                .WithLogger(logger)
                .AddModuleRegistry<UserAccessServiceRegistry>()
                .Build<CliApp>();

            var uam = app.UserAccessModule;

            var bob = await uam.ExecuteCommandAsync(new CreateUserCommand("Bob", "Bob@email.com"));
            var alice = await uam.ExecuteCommandAsync(new CreateUserCommand("Alice", "alice@email.com"));

            DisplayResult(bob);
            DisplayResult(alice);
        }

        private static void DisplayResult(CreateUserResponse bob)
        {
            if (bob.Success)
                Console.WriteLine($"Created user with Id {bob.Id}");
            else
                Console.WriteLine($"Could not create user: {bob.Error}");
        }

        private static IConfiguration GetConfig() =>
            new ConfigurationBuilder()
                .AddJsonFile(Environment.CurrentDirectory + "appSetting.json", optional: true)
                .Build();

        private static Framework.Logging.ILogger SetupLogger(IConfiguration config) =>
            new Framework.Infrastructure.Logging.Logger(
                new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .CreateLogger());
    }
}
