﻿using Functional.Either;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XPL.CLI.Application;
using XPL.Framework.Application;
using XPL.Framework.Application.Modules.Contracts;
using XPL.Modules.UserAccess.Application.Users.CreateUser;

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

            var commands = new List<CreateUserCommand>()
            {
                new CreateUserCommand("Alice 123", "alice@email.com"),
                new CreateUserCommand("Alice_123", "alice@email.com"),
                new CreateUserCommand("", "alice@email.com"),
                new CreateUserCommand("Bob", "bobert@email.com"),
                new CreateUserCommand("Bob 88", "bobert@emailcom"),
                new CreateUserCommand("Bob", "bobert.at.email.com"),
            };

            foreach (var cmd in commands)
            {
                WriteInfo($"Attempting to create user {cmd.UserName} with email address {cmd.EmailAddress}");
                var result = await app.ExecuteCommandAsync(cmd);
                DisplayResult(result);
            }
        }

        private static void DisplayResult(Either<CommandError, CreateUserResponse> bob)
        {
            var (success, message) = bob
                .Map(user => (success: true, message: $"User {user.UserName} created with Id {user.Id}"))
                .Reduce(err => (success: false, message: $"Error: {err}"));

            if (success)
                WriteSuccess(message);
            else
                WriteFail(message);
        }

        private static void WriteInfo(string s) => WriteColor(s, ConsoleColor.Cyan);
        private static void WriteSuccess(string s) => WriteColor(s, ConsoleColor.Green);
        private static void WriteFail(string s) => WriteColor(s, ConsoleColor.Red);

        private static void WriteColor(string s, ConsoleColor c)
        {
            Console.ForegroundColor = c;
            Console.WriteLine(s);
            Console.ResetColor();
        }
    }
}
