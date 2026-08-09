using System.CommandLine;
using System.Text.Json;
using Mixology.Cli;
using Mixology.Kernel.Errors;
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

    [Fact]
    public async Task ErrorAdapterPreservesTypedMappingsAndHidesInternalDetail()
    {
        StringWriter invalidOutput = new();
        StringWriter internalOutput = new();

        int invalid = await CliErrorAdapter.WriteAsync(invalidOutput, AppError.Invalid("name is required"));
        int internalCode = await CliErrorAdapter.WriteAsync(
            internalOutput,
            AppError.Internal("database password leaked"));

        Assert.Equal(ErrorCatalog.ExitInvalid, invalid);
        Assert.Equal("name is required", invalidOutput.ToString().Trim());
        Assert.Equal(ErrorCatalog.ExitInternal, internalCode);
        Assert.Equal("internal error", internalOutput.ToString().Trim());
    }

    [Fact]
    public async Task CancellationAndUnknownFailuresRemainNonApplicationOutcomes()
    {
        StringWriter cancellationOutput = new();
        StringWriter unknownOutput = new();

        int cancellation = await CliErrorAdapter.WriteAsync(
            cancellationOutput,
            new InvalidOperationException("outer", new TaskCanceledException()));
        int unknown = await CliErrorAdapter.WriteAsync(
            unknownOutput,
            new IOException("secret path"));

        Assert.Equal(ErrorCatalog.ExitGeneral, cancellation);
        Assert.Equal("operation cancelled", cancellationOutput.ToString().Trim());
        Assert.Equal(ErrorCatalog.ExitGeneral, unknown);
        Assert.Equal("internal error", unknownOutput.ToString().Trim());
    }

    [Fact]
    public async Task IngredientCommandsPersistAcrossIndependentCliInvocations()
    {
        string root = Path.Combine(Path.GetTempPath(), "mixology-cli-ingredients", Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "mixology.db");
        StringWriter createOutput = new();
        StringWriter createError = new();
        StringWriter listOutput = new();
        StringWriter listError = new();

        try
        {
            int created = await CliApplication.Build(createOutput, createError).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "ingredients", "create", "House Gin",
                "--category", "spirit",
                "--unit", "oz",
                "--description", "Dry",
            ]).InvokeAsync();
            int listed = await CliApplication.Build(listOutput, listError).Parse(
            [
                "--db", database,
                "--actor", "anonymous",
                "ingredients", "list",
                "--json",
            ]).InvokeAsync();

            Assert.Equal(0, created);
            Assert.Equal(0, listed);
            Assert.StartsWith("ing-", createOutput.ToString().Trim(), StringComparison.Ordinal);
            Assert.Empty(createError.ToString());
            Assert.Empty(listError.ToString());
            using JsonDocument document = JsonDocument.Parse(listOutput.ToString());
            JsonElement item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal("House Gin", item.GetProperty("name").GetString());
            Assert.Equal("spirit", item.GetProperty("category").GetString());
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
    public async Task AuditCliObservesACommandFromAnEarlierInvocation()
    {
        string root = Path.Combine(Path.GetTempPath(), "mixology-cli-audit", Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "mixology.db");
        StringWriter auditOutput = new();
        StringWriter auditError = new();

        try
        {
            int created = await CliApplication.Build(TextWriter.Null, TextWriter.Null).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "ingredients", "create", "Gin",
                "--category", "spirit",
                "--unit", "oz",
            ]).InvokeAsync();
            int audited = await CliApplication.Build(auditOutput, auditError).Parse(
            [
                "--db", database,
                "--actor", "owner",
                "audit", "list",
                "--json",
            ]).InvokeAsync();

            Assert.Equal(0, created);
            Assert.Equal(0, audited);
            Assert.Empty(auditError.ToString());
            using JsonDocument document = JsonDocument.Parse(auditOutput.ToString());
            JsonElement entry = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal("Mixology::Actor::\"manager\"", entry.GetProperty("principal").GetString());
            Assert.Contains("Ingredient::Action", entry.GetProperty("action").GetString(), StringComparison.Ordinal);
            Assert.True(entry.GetProperty("success").GetBoolean());
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
}
