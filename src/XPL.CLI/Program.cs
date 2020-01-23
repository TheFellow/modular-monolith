using Functional.Either;
using System;
using System.Threading.Tasks;
using XPL.CLI.Application;
using XPL.Framework.Domain.Contracts;
using XPL.Modules.UserAccess.Application.UseCases.UserRegistrations.ConfirmRegistration;
using XPL.Modules.UserAccess.Application.UseCases.UserRegistrations.NewUserRegistration;
using XPL.Modules.UserAccess.Application.UseCases.Users.UpdatePassword;

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

            //await RegisterAlice(app);
            //await RegisterBob(app);

            //await ConfirmAlice(app);
            await UpdateAlicesPassword(app);
        }
        private static async Task RegisterAlice(CliApp app)
        {
            WriteInfo("Create Registration for Alice.");

            var cmd = new NewUserRegistrationCommand("Alice", "passw0rd", "alice@email.com", "Alice", "Brown");
            var result = await app.ExecuteCommandAsync(cmd);
            DisplayResult(result, r => $"Registered login \"{r.Login}\" id {r.RegistrationId}");
        }

        private static async Task ConfirmAlice(CliApp app)
        {
            WriteInfo("Confirming Alice.");

            var cmd = new ConfirmRegistrationCommand(new Guid("63BA8AE9-FC04-4F3A-BE0E-0CE3D496A463"), "abc123");
            var result = await app.ExecuteCommandAsync(cmd);

            DisplayResult(result, r => r.Message);
        }

        private static async Task UpdateAlicesPassword(CliApp app)
        {
            WriteInfo("Updating Alice's password.");

            var cmd = new UpdatePasswordCommand("Alice", "passw0rd", "p@ssw0rd");
            var result = await app.ExecuteCommandAsync(cmd);

            DisplayResult(result, r => r.Message);
        }

        private static async Task RegisterBob(CliApp app)
        {
            WriteInfo("Confirm registration for Bob.");

            var cmd = new NewUserRegistrationCommand("Bob", "passw0rd", "Bob@email.com", "Robert", "Brown");
            var result = await app.ExecuteCommandAsync(cmd);
            DisplayResult(result, r => $"Registered login \"{r.Login}\" id {r.RegistrationId}");
        }

        private static void DisplayResult<T>(Either<CommandError, T> result, Func<T, string> display)
        {
            (bool success, string message) = result
                .Map(resp => (true, display(resp)))
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
