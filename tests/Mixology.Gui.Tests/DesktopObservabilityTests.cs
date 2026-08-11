using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mixology.Kernel.Errors;
using Xunit;

namespace Mixology.Gui.Tests;

[Collection(nameof(DesktopEnvironmentCollection))]
public sealed class DesktopObservabilityTests
{
    private static readonly Action<ILogger, Exception?> LogInformationMarker = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(1, "DesktopObservabilityInformation"),
        "desktop observability marker");
    private static readonly Action<ILogger, Exception?> LogExcludedMarker = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(2, "DesktopObservabilityExcluded"),
        "excluded desktop marker");
    private static readonly Action<ILogger, Exception?> LogWarningMarker = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(3, "DesktopObservabilityWarning"),
        "included desktop marker");

    [Fact]
    public async Task JsonFileLoggingIsHostOwnedAndReleasesTheFile()
    {
        string root = TemporaryDirectory();
        string log = Path.Combine(root, "desktop.jsonl");
        try
        {
            await using (DesktopHost host = await DesktopHost.OpenAsync(
                DesktopOptions.Create(
                    Path.Combine(root, "mixology.db"),
                    "owner",
                    "debug",
                    "json",
                    log,
                    metrics: false),
                TestContext.Current.CancellationToken))
            {
                LogInformationMarker(
                    host.Services.GetRequiredService<ILogger<DesktopObservabilityTests>>(),
                    null);
            }

            string[] lines = await File.ReadAllLinesAsync(log, TestContext.Current.CancellationToken);
            Assert.Contains(lines, line => line.Contains("desktop observability marker", StringComparison.Ordinal));
            foreach (string line in lines)
            {
                using JsonDocument document = JsonDocument.Parse(line);
                Assert.True(document.RootElement.TryGetProperty("Level", out _));
            }

            using FileStream exclusive = new(log, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(exclusive.Length > 0);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task TextFileLoggingUsesTheConfiguredLevelAndFormat()
    {
        string root = TemporaryDirectory();
        string log = Path.Combine(root, "desktop.log");
        try
        {
            await using (DesktopHost host = await DesktopHost.OpenAsync(
                DesktopOptions.Create(
                    Path.Combine(root, "mixology.db"),
                    "owner",
                    "warn",
                    "text",
                    log,
                    metrics: false),
                TestContext.Current.CancellationToken))
            {
                ILogger<DesktopObservabilityTests> logger =
                    host.Services.GetRequiredService<ILogger<DesktopObservabilityTests>>();
                LogExcludedMarker(logger, null);
                LogWarningMarker(logger, null);
            }

            string diagnostics = await File.ReadAllTextAsync(log, TestContext.Current.CancellationToken);
            Assert.DoesNotContain("excluded desktop marker", diagnostics, StringComparison.Ordinal);
            Assert.Contains("[WRN]", diagnostics, StringComparison.Ordinal);
            Assert.Contains("included desktop marker", diagnostics, StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task MetricsExporterPortIsReleasedWithEachDesktopHost()
    {
        string root = TemporaryDirectory();
        try
        {
            for (int invocation = 0; invocation < 2; invocation++)
            {
                await using DesktopHost host = await DesktopHost.OpenAsync(
                    DesktopOptions.Create(
                        Path.Combine(root, "mixology.db"),
                        "owner",
                        "error",
                        "text",
                        Path.Combine(root, $"desktop-{invocation}.log"),
                        metrics: true),
                    TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task DirectoryLogDestinationRemainsATypedInvalidError()
    {
        string root = TemporaryDirectory();
        try
        {
            InvalidError error = await Assert.ThrowsAsync<InvalidError>(async () =>
                await DesktopHost.OpenAsync(
                    DesktopOptions.Create(
                        Path.Combine(root, "mixology.db"),
                        "owner",
                        "info",
                        "text",
                        root,
                        metrics: false),
                    TestContext.Current.CancellationToken));

            Assert.Equal("log file path names a directory", error.UserMessage);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void EnvironmentDefaultsReachTheRuntimeAndInvalidMetricsIsTyped()
    {
        string root = TemporaryDirectory();
        string log = Path.Combine(root, "environment.jsonl");
        string? oldLevel = Environment.GetEnvironmentVariable("MIXOLOGY_LOG_LEVEL");
        string? oldFormat = Environment.GetEnvironmentVariable("MIXOLOGY_LOG_FORMAT");
        string? oldFile = Environment.GetEnvironmentVariable("MIXOLOGY_LOG_FILE");
        string? oldMetrics = Environment.GetEnvironmentVariable("MIXOLOGY_METRICS");
        try
        {
            Environment.SetEnvironmentVariable("MIXOLOGY_LOG_LEVEL", "debug");
            Environment.SetEnvironmentVariable("MIXOLOGY_LOG_FORMAT", "json");
            Environment.SetEnvironmentVariable("MIXOLOGY_LOG_FILE", log);
            Environment.SetEnvironmentVariable("MIXOLOGY_METRICS", "true");
            RecordingRuntime runtime = new();

            int exit = DesktopCommandLine.Build(runtime).Parse([
                "--db", Path.Combine(root, "mixology.db"),
            ]).Invoke();

            Assert.Equal(ErrorCatalog.ExitSuccess, exit);
            Assert.Equal(Serilog.Events.LogEventLevel.Debug, runtime.Options?.LogLevel);
            Assert.Equal("json", runtime.Options?.LogFormat);
            Assert.Equal(log, runtime.Options?.LogFile);
            Assert.True(runtime.Options!.Metrics);

            Environment.SetEnvironmentVariable("MIXOLOGY_METRICS", "sometimes");
            InvalidError invalid = Assert.Throws<InvalidError>(() =>
                DesktopCommandLine.Build(new RecordingRuntime()).Parse([
                    "--db", Path.Combine(root, "other.db"),
                ]));
            Assert.Equal(
                "environment variable MIXOLOGY_METRICS must be true or false",
                invalid.UserMessage);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MIXOLOGY_LOG_LEVEL", oldLevel);
            Environment.SetEnvironmentVariable("MIXOLOGY_LOG_FORMAT", oldFormat);
            Environment.SetEnvironmentVariable("MIXOLOGY_LOG_FILE", oldFile);
            Environment.SetEnvironmentVariable("MIXOLOGY_METRICS", oldMetrics);
            Cleanup(root);
        }
    }

    private static string TemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "mixology-desktop-observability-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string path)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class RecordingRuntime : IDesktopRuntime
    {
        public DesktopOptions? Options { get; private set; }

        public int Run(DesktopOptions options)
        {
            Options = options;
            return ErrorCatalog.ExitSuccess;
        }
    }
}

[CollectionDefinition(nameof(DesktopEnvironmentCollection), DisableParallelization = true)]
public sealed class DesktopEnvironmentCollection;
