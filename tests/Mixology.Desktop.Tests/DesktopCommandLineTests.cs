using Mixology.Application.Authentication;
using Mixology.Kernel.Errors;
using Serilog.Events;
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

    [Fact]
    public void ObservabilityOptionsAreStronglyParsedBeforeRuntime()
    {
        RecordingRuntime runtime = new();
        string log = Path.Combine(Path.GetTempPath(), "mixology-desktop", "diagnostics.jsonl");

        int exit = DesktopCommandLine.Build(runtime).Parse([
            "--log-level", "warning",
            "--log-format", "json",
            "--log-file", log,
            "--metrics",
        ]).Invoke();

        Assert.Equal(ErrorCatalog.ExitSuccess, exit);
        Assert.Equal(LogEventLevel.Warning, runtime.Options?.LogLevel);
        Assert.Equal("json", runtime.Options?.LogFormat);
        Assert.Equal(log, runtime.Options?.LogFile);
        Assert.True(runtime.Options!.Metrics);
    }

    [Theory]
    [InlineData("--log-level", "trace", "invalid log level \"trace\"")]
    [InlineData("--log-format", "yaml", "invalid log format \"yaml\"")]
    public void InvalidObservabilityOptionsUseTypedInvalidExit(
        string option,
        string value,
        string expected)
    {
        RecordingRuntime runtime = new();
        StringWriter errors = new();

        int exit = DesktopCommandLine.Build(runtime, errors).Parse([option, value]).Invoke();

        Assert.Equal(ErrorCatalog.ExitInvalid, exit);
        Assert.Equal(expected, errors.ToString().Trim());
        Assert.Equal(0, runtime.Calls);
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
