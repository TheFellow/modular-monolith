using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using XPL.CLI.Application;
using XPL.Framework.Application;
using XPL.Framework.Infrastructure.Configuration;
using XPL.Framework.Infrastructure.Logging;
using XPL.Modules.UserAccess.Startup;
using XPL.Modules.UserAccess.Users.CreateUser;

namespace XPL.CLI
{
    class Program
    {
        static async Task Main()
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

        private static IConfiguration GetConfig() => ConfigurationFactory.OptionalAppSettingsJson;

        private static Framework.Logging.ILogger SetupLogger(IConfiguration _) => LoggerFactory.ConsoleInfoLogger;
            
    }
}
