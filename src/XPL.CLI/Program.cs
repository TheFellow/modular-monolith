﻿using Functional.Either;
using System;
using System.Threading.Tasks;
using XPL.CLI.Application;
using XPL.Framework.Application.Contracts;
using XPL.Modules.UserAccess.Application.UseCases.Registrations.ConfirmRegistration;
using XPL.Modules.UserAccess.Application.UseCases.Registrations.NewUserRegistration;
using XPL.Modules.UserAccess.Application.UseCases.Users.UpdateEmail;
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

            await ConfirmAlice(app);
            await UpdateAlicesPassword(app);
            await UpdateAlicesEmail(app);
            await UpdateAlicesEmailAgain(app);

            await RegisterCharles(app);
        }


        private static async Task RegisterAlice(CliApp app)
        {
            WriteInfo("Create Registration for Alice.");

            var cmd = new NewRegistrationCommand("Alice", "passw0rd", "alice@email.com", "Alice", "Brown");
            var result = await app.ExecuteCommandAsync(cmd);
            DisplayResult(result, r => $"Registered login \"{r.Login}\" id {r.RegistrationId}");
        }

        private static async Task ConfirmAlice(CliApp app)
        {
            WriteInfo("Confirming Alice.");

            var cmd = new ConfirmRegistrationCommand(new Guid("6A3A3891-7D14-4A21-9E5C-F5D2001B7D01"), "abc123");
            var result = await app.ExecuteCommandAsync(cmd);

            DisplayResult(result, r => r);
        }

        private static async Task UpdateAlicesPassword(CliApp app)
        {
            WriteInfo("Updating Alice's password.");

            var cmd = new UpdatePasswordCommand("Alice", "passw0rd", "p@ssw0rd");
            var result = await app.ExecuteCommandAsync(cmd);

            DisplayResult(result, r => r);
        }

        private static async Task UpdateAlicesEmail(CliApp app)
        {
            WriteInfo("Updating Alice's Email");

            var cmd = new UpdateEmailCommand("Alice", "alice.brown@email.com");
            var result = await app.ExecuteCommandAsync(cmd);

            DisplayResult(result, r => r);
        }

        private static async Task UpdateAlicesEmailAgain(CliApp app)
        {
            WriteInfo("Updating Alice's Email Again");

            var cmd = new UpdateEmailCommand("Alice", "alice@email.com");
            var result = await app.ExecuteCommandAsync(cmd);

            DisplayResult(result, r => r);
        }

        private static async Task RegisterBob(CliApp app)
        {
            WriteInfo("Confirm registration for Bob.");

            var cmd = new NewRegistrationCommand("Bob", "passw0rd", "Bob@email.com", "Robert", "Brown");
            var result = await app.ExecuteCommandAsync(cmd);
            DisplayResult(result, r => $"Registered login \"{r.Login}\" id {r.RegistrationId}");
        }

        private static async Task RegisterCharles(CliApp app)
        {
            WriteInfo("Confirm registration for Charles.");

            var cmd = new NewRegistrationCommand("Charles", "passw0rd", "alice.brown@email.com", "Charles", "Brown");
            var result = await app.ExecuteCommandAsync(cmd);
            DisplayResult(result, r => $"Registered login \"{r.Login}\" id {r.RegistrationId}");
        }

        private static void DisplayResult<T>(Result<T> result, Func<T, string> display) =>
            result
                .OnOk(r => WriteSuccess(display(r)))
                .OnFail(r => WriteFail(r.Message));

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
