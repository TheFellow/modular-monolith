using System.Text.Json;
using Mixology.Cli;
using Mixology.Kernel.Errors;
using Xunit;

namespace Mixology.Cli.Tests;

public sealed class ObservabilityCliTests
{
    [Fact]
    public async Task LogConfigurationIsInvocationScopedAndNeverContaminatesJsonStdout()
    {
        string root = TemporaryDirectory();
        string database = Path.Combine(root, "mixology.db");
        string jsonLog = Path.Combine(root, "diagnostics.jsonl");
        string textLog = Path.Combine(root, "diagnostics.log");
        StringWriter jsonOutput = new();
        StringWriter jsonError = new();
        StringWriter textOutput = new();
        StringWriter textError = new();

        try
        {
            int jsonExit = await CliApplication.Build(jsonOutput, jsonError).Parse(
            [
                "--db", database,
                "--log-level", "debug",
                "--log-format", "json",
                "--log-file", jsonLog,
                "ingredients", "list", "--json",
            ]).InvokeAsync();
            using JsonDocument commandOutput = JsonDocument.Parse(jsonOutput.ToString());
            Assert.Equal(JsonValueKind.Array, commandOutput.RootElement.GetProperty("items").ValueKind);
            Assert.Equal(0, jsonExit);
            Assert.Empty(jsonError.ToString());
            string[] lines = await File.ReadAllLinesAsync(jsonLog);
            Assert.NotEmpty(lines);
            Assert.All(lines, line =>
            {
                using JsonDocument diagnostic = JsonDocument.Parse(line);
                Assert.True(diagnostic.RootElement.TryGetProperty("Level", out _));
            });

            using (FileStream exclusive = new(
                jsonLog,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                Assert.True(exclusive.Length > 0);
            }

            int textExit = await CliApplication.Build(textOutput, textError).Parse(
            [
                "--db", database,
                "--log-level", "info",
                "--log-format", "text",
                "--log-file", textLog,
                "status",
            ]).InvokeAsync();

            Assert.Equal(0, textExit);
            Assert.Equal("Mixology foundation is ready.", textOutput.ToString().Trim());
            Assert.Empty(textError.ToString());
            Assert.Contains("[INF]", await File.ReadAllTextAsync(textLog), StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Theory]
    [InlineData("--log-level", "trace", "invalid log level \"trace\"")]
    [InlineData("--log-format", "yaml", "invalid log format \"yaml\"")]
    public async Task InvalidLoggingOptionsUseTypedInvalidExit(
        string option,
        string value,
        string expected)
    {
        string root = TemporaryDirectory();
        StringWriter output = new();
        StringWriter error = new();

        try
        {
            int exitCode = await CliApplication.Build(output, error).Parse(
            [
                "--db", Path.Combine(root, "mixology.db"),
                option, value,
                "status",
            ]).InvokeAsync();

            Assert.Equal(ErrorCatalog.ExitInvalid, exitCode);
            Assert.Empty(output.ToString());
            Assert.Equal(expected, error.ToString().Trim());
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task DirectoryLogDestinationUsesTypedInvalidExit()
    {
        string root = TemporaryDirectory();
        StringWriter output = new();
        StringWriter error = new();

        try
        {
            int exitCode = await CliApplication.Build(output, error).Parse(
            [
                "--db", Path.Combine(root, "mixology.db"),
                "--log-file", root,
                "status",
            ]).InvokeAsync();

            Assert.Equal(ErrorCatalog.ExitInvalid, exitCode);
            Assert.Empty(output.ToString());
            Assert.Equal("log file path names a directory", error.ToString().Trim());
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task MetricsExporterReleasesItsPortBetweenInvocations()
    {
        string root = TemporaryDirectory();
        string database = Path.Combine(root, "mixology.db");

        try
        {
            for (int invocation = 0; invocation < 2; invocation++)
            {
                StringWriter output = new();
                StringWriter error = new();
                int exitCode = await CliApplication.Build(output, error).Parse(
                [
                    "--db", database,
                    "--log-level", "error",
                    "--metrics",
                    "status",
                ]).InvokeAsync();

                Assert.Equal(0, exitCode);
                Assert.Equal("Mixology foundation is ready.", output.ToString().Trim());
                Assert.Empty(error.ToString());
            }
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string TemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "mixology-cli-observability-tests",
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
}
