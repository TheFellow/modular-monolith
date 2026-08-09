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
        Assert.Equal("Initialize storage and report foundation readiness.", status.Description);
    }

    [Fact]
    public async Task StatusInitializesTheConfiguredDatabaseWithoutWritingDiagnostics()
    {
        string root = Path.Combine(Path.GetTempPath(), "mixology-cli-tests", Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "mixology.db");
        StringWriter output = new();
        StringWriter error = new();

        try
        {
            int exitCode = await CliApplication.Build(output, error)
                .Parse(["--db", database, "--actor", "owner", "status"])
                .InvokeAsync();

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(database));
            Assert.Equal("Mixology foundation is ready.", output.ToString().Trim());
            Assert.Empty(error.ToString());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TypedInvalidInputUsesTheSharedExitCodeAndSafeMessage()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await CliApplication.Build(output, error)
            .Parse(["--actor", "visitor", "status"])
            .InvokeAsync();

        Assert.Equal(10, exitCode);
        Assert.Empty(output.ToString());
        Assert.Equal("unknown actor: \"visitor\"", error.ToString().Trim());
    }
}
