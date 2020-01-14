using Functional.Either;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XPL.CLI.Application;
using XPL.Framework.Application;
using XPL.Framework.Modules.Contracts;
using XPL.Modules.UserAccess.Application.UserRegistrations.RegisterNewUser;

namespace XPL.CLI
{
    class Program
    {
        static async Task Main()
        {
            var runner = CliApp
                .Build();

            App app;

            try
            {
                app = runner.Run();
            }
            catch (Exception ex)
            {
                runner.Logger.Fatal(ex, "A fatal error occurred starting the application.");
                throw;
            }

            await Run(app);
        }

        private static async Task Run(App app)
        {
            app.Logger.Info("Application {@AppInfo} Started.", app.AppInfo);

            await TestRegisterNewUserCommands(app);
        }

        private static async Task TestRegisterNewUserCommands(App app)
        {
            WriteInfo(Environment.NewLine + nameof(TestRegisterNewUserCommands));

            var commands = new List<RegisterNewUserCommand>()
            {
                new RegisterNewUserCommand("alice", "pword", "alice@email.com", "Alice", "Brown"),
                new RegisterNewUserCommand("", "password", "alice@email.com", "Alice", "Brown"),
                new RegisterNewUserCommand("bob", "password", "bobbert@email.com", "Bob", "Carson"),
                new RegisterNewUserCommand("bob", "pass123", "bobbert@email.com", "Bob", "Carson"),
                new RegisterNewUserCommand("bob", "password123", "bobbert@email.com", "Bob", "Carson"),
            };

            foreach (var cmd in commands)
            {
                WriteInfo($"Attempting to create login {cmd.Login} for {cmd.FirstName} {cmd.LastName} ({cmd.Email})");
                var result = await app.ExecuteCommandAsync(cmd);
                DisplayResult<RegisterNewUserResponse>(result, r => $"Created registration for login {cmd.Login}. Registration Expires {r.ExpiryDate.ToString("MM/dd/yyyy")}.");
            }
        }

        private static void DisplayResult<T>(Either<CommandError, T> response, Func<T, string> func)
        {
            var (success, message) = response
                .Map(user => (success: true, message: func(user)))
                .Reduce(err => (success: false, message: $"Error: {err}"));

            if (success)
                WriteSuccess(message);
            else
                WriteFail(message);
        }

        private static void WriteInfo(string s) => WriteColor(s, ConsoleColor.Cyan);
        private static void WriteSuccess(string s) => WriteColor("  " + s, ConsoleColor.Green);
        private static void WriteFail(string s) => WriteColor("  " + s, ConsoleColor.Red);

        private static void WriteColor(string s, ConsoleColor c)
        {
            Console.ForegroundColor = c;
            Console.WriteLine(s);
            Console.ResetColor();
        }
    }
}
