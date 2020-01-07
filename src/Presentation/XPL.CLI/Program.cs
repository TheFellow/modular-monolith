using System;
using System.Threading.Tasks;
using XPL.CLI.Application;
using XPL.Framework.Application;
using XPL.Modules.UserAccess.Application.Users.CreateUser;

namespace XPL.CLI
{
    class Program
    {
        static async Task Main()
        {
            var runner = CliApp
                .Build();

            try
            {
                var app = runner.Run();
                await Run(app);
            }
            catch (Exception ex)
            {
                runner.Logger.Fatal(ex, "A fatal error occurred starting the application.");
            }
        }

        private static async Task Run(App app)
        {
            app.Logger.Info("Application {@AppInfo} Started.", app.AppInfo);

            var bob = await app.ExecuteCommandAsync(new CreateUserCommand("Bob", "Bob@email.com"));
            var alice = await app.ExecuteCommandAsync(new CreateUserCommand("Alice", "alice@email.com"));

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
    }
}
