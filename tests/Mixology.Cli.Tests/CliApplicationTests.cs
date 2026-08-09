using System.CommandLine;
using Mixology.Cli;
using Xunit;

namespace Mixology.Cli.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public void Build_RegistersStatusCommand()
    {
        RootCommand root = CliApplication.Build();

        Command status = Assert.Single(root.Subcommands, command => command.Name == "status");
        Assert.Equal("Report whether the application foundation is available.", status.Description);
    }
}

