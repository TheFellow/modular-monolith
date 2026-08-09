using Mixology.Application.Authentication;
using Mixology.Kernel.Errors;
using Xunit;

namespace Mixology.Desktop.Tests;

public sealed class DesktopCommandLineTests
{
    [Fact]
    public void HelpDoesNotBootstrapDesktopRuntime()
    {
        RecordingRuntime runtime = new();
        StringWriter output = new();
        System.CommandLine.ParseResult parsed = DesktopCommandLine.Build(runtime).Parse(["--help"]);
        parsed.InvocationConfiguration.Output = output;

        int exit = parsed.Invoke();

        Assert.Equal(0, exit);
        Assert.Equal(0, runtime.Calls);
        Assert.Contains("Desktop client for Mixology", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ActorAliasAndDatabaseAreStronglyParsedBeforeRuntime()
    {
        RecordingRuntime runtime = new();
        string database = Path.Combine(Path.GetTempPath(), "mixology-desktop", "test.db");

        int exit = DesktopCommandLine.Build(runtime).Parse(["--db", database, "--as", "manager"]).Invoke();

        Assert.Equal(ErrorCatalog.ExitSuccess, exit);
        Assert.Equal(Actor.Manager, runtime.Options?.Actor);
        Assert.Equal(Path.GetFullPath(database), runtime.Options?.DatabasePath);
    }

    private sealed class RecordingRuntime : IDesktopRuntime
    {
        public int Calls { get; private set; }
        public DesktopOptions? Options { get; private set; }

        public int Run(DesktopOptions options)
        {
            Calls++;
            Options = options;
            return ErrorCatalog.ExitSuccess;
        }
    }
}
