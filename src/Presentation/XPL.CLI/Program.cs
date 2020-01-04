using System;
using System.Threading.Tasks;
using XPL.CLI.Application;
using XPL.Modules.UserAccess.Users.CreateUser;

namespace XPL.CLI
{
    class Program
    {
        static async Task Main()
        {
            var app = CliApp.Build();

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
