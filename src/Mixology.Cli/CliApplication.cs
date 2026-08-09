using System.CommandLine;

namespace Mixology.Cli;

public static class CliApplication
{
    public static RootCommand Build()
    {
        RootCommand root = new("Manage the Mixology modular-monolith application.");

        Command status = new("status", "Report whether the application foundation is available.");
        status.SetAction(_ =>
        {
            Console.Out.WriteLine("Mixology foundation is ready.");
            return 0;
        });

        root.Subcommands.Add(status);
        return root;
    }
}

