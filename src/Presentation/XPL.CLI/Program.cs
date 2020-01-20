﻿using Functional.Either;
using System;
using System.Threading.Tasks;
using XPL.CLI.Application;
using XPL.Framework.Modules.Contracts;
using XPL.Modules.UserAccess.Application.UserRegistrations.ConfirmRegistration;

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

            //using (var userAccess = app.GetUserAccessUoW())
            //{
            //    WriteInfo("Create Registration for Alice.");

            //    var cmd = new NewUserRegistrationCommand("Alice", "passw0rd", "alice@email.com", "Alice", "Brown");
            //    var result = await userAccess.ExecuteCommandAsync(cmd);
            //    DisplayResult(result, r => $"Registered login \"{r.Login}\" id {r.RegistrationId}");

            //    await userAccess.CommitAsync();
            //}


            WriteInfo("Confirm registration for Alice.");

            var cmd = new ConfirmRegistrationCommand(new Guid("5F0D2543-D87F-49F9-B47F-6B7D9C41F1B1"), "abc123");
            var result = await app.ExecuteCommandAsync(cmd);
            DisplayResult(result, r => r.Message);



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
