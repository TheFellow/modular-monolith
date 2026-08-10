using System.CommandLine;
using Mixology.Application.Authentication;
using Mixology.Kernel.Errors;
using Xunit;

namespace Mixology.Tui.Tests;

[Collection("TUI environment")]
public sealed class TuiApplicationTests
{
    [Fact]
    public async Task HelpDoesNotBootstrapTheRuntime()
    {
        RecordingRuntime runtime = new();
        StringWriter output = new();
        ParseResult parsed = TuiApplication.Build(runtime).Parse(["--help"]);
        parsed.InvocationConfiguration.Output = output;

        int exit = await parsed.InvokeAsync();

        Assert.Equal(0, exit);
        Assert.Equal(0, runtime.Calls);
        Assert.Contains("Interactive terminal client for Mixology", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OptionsUseAsAliasAndDefaultLogBesideDatabase()
    {
        RecordingRuntime runtime = new();
        string root = Path.Combine(Path.GetTempPath(), "mixology-tui-options", Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "data.db");

        int exit = await TuiApplication.Build(runtime)
            .Parse(["--db", database, "--as", "manager", "--log-level", "warn", "--log-format", "json"])
            .InvokeAsync();

        Assert.Equal(0, exit);
        Assert.Equal(1, runtime.Calls);
        Assert.Equal(Actor.Manager, runtime.Options?.Actor);
        Assert.Equal(Path.GetFullPath(database), runtime.Options?.DatabasePath);
        Assert.Equal(Path.Combine(root, "mixology-tui.log"), runtime.Options?.LogFile);
        Assert.Equal("json", runtime.Options?.LogFormat);
    }

    [Fact]
    public void OptionsDefaultToReferenceDataPath()
    {
        TuiOptions options = TuiOptions.Create(null, "owner", "info", "text", null, metrics: false);

        Assert.Equal(Path.GetFullPath(Path.Combine("data", "mixology.db")), options.DatabasePath);
        Assert.Equal(Path.GetFullPath(Path.Combine("data", "mixology-tui.log")), options.LogFile);
    }

    [Fact]
    public async Task DatabaseAndActorDefaultsHonorEnvironmentWithExplicitOptionsTakingPrecedence()
    {
        string root = Path.Combine(Path.GetTempPath(), "mixology-tui-environment", Guid.NewGuid().ToString("N"));
        string environmentDatabase = Path.Combine(root, "environment.db");
        string explicitDatabase = Path.Combine(root, "explicit.db");
        string? priorDatabase = Environment.GetEnvironmentVariable("MIXOLOGY_DB");
        string? priorActor = Environment.GetEnvironmentVariable("MIXOLOGY_ACTOR");
        try
        {
            Environment.SetEnvironmentVariable("MIXOLOGY_DB", environmentDatabase);
            Environment.SetEnvironmentVariable("MIXOLOGY_ACTOR", "bartender");
            RecordingRuntime environment = new();
            RecordingRuntime explicitValues = new();

            Assert.Equal(0, await TuiApplication.Build(environment).Parse([]).InvokeAsync());
            Assert.Equal(0, await TuiApplication.Build(explicitValues)
                .Parse(["--db", explicitDatabase, "--actor", "manager"]).InvokeAsync());

            Assert.Equal(Path.GetFullPath(environmentDatabase), environment.Options?.DatabasePath);
            Assert.Equal(Actor.Bartender, environment.Options?.Actor);
            Assert.Equal(Path.GetFullPath(explicitDatabase), explicitValues.Options?.DatabasePath);
            Assert.Equal(Actor.Manager, explicitValues.Options?.Actor);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MIXOLOGY_DB", priorDatabase);
            Environment.SetEnvironmentVariable("MIXOLOGY_ACTOR", priorActor);
        }
    }

    [Fact]
    public async Task TypedCancellationAndUnknownFailuresAreSafe()
    {
        StringWriter typedError = new();
        StringWriter cancellationError = new();
        StringWriter unknownError = new();

        int typed = await TuiApplication.Build(
            new ThrowingRuntime(AppError.Invalid("bad actor")),
            typedError).Parse([]).InvokeAsync();
        int cancellation = await TuiApplication.Build(
            new ThrowingRuntime(new TaskCanceledException("stopped")),
            cancellationError).Parse([]).InvokeAsync();
        int unknown = await TuiApplication.Build(
            new ThrowingRuntime(new IOException("secret path")),
            unknownError).Parse([]).InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitInvalid, typed);
        Assert.Equal("bad actor", typedError.ToString().Trim());
        Assert.Equal(ErrorCatalog.ExitGeneral, cancellation);
        Assert.Equal("operation cancelled", cancellationError.ToString().Trim());
        Assert.Equal(ErrorCatalog.ExitGeneral, unknown);
        Assert.Equal("internal error", unknownError.ToString().Trim());
    }

    private sealed class RecordingRuntime : ITuiRuntime
    {
        public int Calls { get; private set; }
        public TuiOptions? Options { get; private set; }

        public Task RunAsync(TuiOptions options, CancellationToken cancellationToken = default)
        {
            Calls++;
            Options = options;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingRuntime(Exception exception) : ITuiRuntime
    {
        public Task RunAsync(TuiOptions options, CancellationToken cancellationToken = default) =>
            Task.FromException(exception);
    }
}

[CollectionDefinition("TUI environment", DisableParallelization = true)]
public sealed class TuiEnvironmentCollection;
