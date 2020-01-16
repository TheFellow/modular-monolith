using Functional.Either;
using System;
using System.Threading.Tasks;
using XPL.CLI.Application;
using XPL.Framework.Modules.Contracts;
using XPL.Modules.UserAccess.Application.UserRegistrations.NewUserRegistration;

namespace XPL.CLI
{
    class Program
    {
        static async Task Main()
        {
            var runner = CliApp
                .Build();

            CliApp app;

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

        private static async Task Run(CliApp app)
        {
            app.Logger.Info("Application {@AppInfo} Started.", app.AppInfo);

            using (var userAccess = app.GetUserAccessUoW())
            {
                WriteInfo("Create Registration for Alice.");

                var cmd = new NewUserRegistrationCommand("Alice", "passw0rd", "alice@email.com", "Alice", "Brown");
                var result = await userAccess.ExecuteCommandAsync(cmd);
                DisplayResult(result);

                WriteInfo("Create Registration for Bob.");

                cmd = new NewUserRegistrationCommand("Bob", "passw0rd", "bob@email.com", "Bob", "Crown");
                result = await userAccess.ExecuteCommandAsync(cmd);

                DisplayResult(result);

                await userAccess.CommitAsync();
            }
        }

        private static void DisplayResult(Either<CommandError, NewUserRegistrationResponse> result)
        {
            (bool success, string message) = result
                .Map(resp => (true, $"Registered login \"{resp.Login}\" id {resp.RegistrationId}"))
                .Reduce(err => (false, err.Error));

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
