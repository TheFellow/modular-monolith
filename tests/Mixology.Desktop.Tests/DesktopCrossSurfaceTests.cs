using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Cli;
using Mixology.Desktop.Workspaces;
using Mixology.Desktop.Workspaces.Ingredients;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Presentation;
using Mixology.Presentation.Mutations;
using Xunit;

namespace Mixology.Desktop.Tests;

public sealed class DesktopCrossSurfaceTests
{
    [Fact]
    public async Task DesktopWorkspaceMutationIsObservedByFreshCliProcess()
    {
        await using TemporaryStore store = new("desktop-to-cli");
        await using (DesktopHost host = await DesktopHost.OpenAsync(
                         DesktopOptions.Create(store.Database, "manager"),
                         TestContext.Current.CancellationToken))
        {
            MixologySession session = host.Services.GetRequiredService<MixologySessionFactory>()
                .Create(Actor.Manager);
            Func<IDesktopWorkspace> factory = IngredientsViewModel.CreateFactory(
                host.Services.GetRequiredService<IngredientsModule>(),
                host.Services.GetRequiredService<IngredientActionProjector>(),
                host.Services.GetRequiredService<TaggedMutationCoordinator>(),
                session,
                Actor.Manager);
            await using IngredientsViewModel workspace = Assert.IsType<IngredientsViewModel>(factory());
            await workspace.ActivateAsync(TestContext.Current.CancellationToken);
            workspace.BeginCreateCommand.Execute(null);
            workspace.EditorName = "Desktop Cross Surface Gin";
            workspace.EditorCategory = "spirit";
            workspace.EditorUnit = "oz";
            workspace.EditorDescription = "created through the Avalonia view model";
            await workspace.SubmitAsync(TestContext.Current.CancellationToken);

            Assert.Null(workspace.Error);
            Assert.Contains(workspace.Items, static item => item.Name == "Desktop Cross Surface Gin");
        }

        SqliteConnection.ClearAllPools();
        ProcessResult cli = await RunCliAsync(
            "--db", store.Database,
            "--actor", "anonymous",
            "--log-level", "error",
            "--log-file", store.CliLog,
            "ingredients", "list",
            "--json");

        Assert.True(cli.ExitCode == 0, cli.StandardError);
        Assert.Empty(cli.StandardError);
        using JsonDocument document = JsonDocument.Parse(cli.StandardOutput);
        JsonElement ingredient = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Desktop Cross Surface Gin", ingredient.GetProperty("name").GetString());
    }

    [Fact]
    public async Task CliProcessMutationIsObservedByFreshDesktopWorkspace()
    {
        await using TemporaryStore store = new("cli-to-desktop");
        ProcessResult cli = await RunCliAsync(
            "--db", store.Database,
            "--actor", "manager",
            "--log-level", "error",
            "--log-file", store.CliLog,
            "ingredients", "create", "Fresh Desktop Vermouth",
            "--category", "other",
            "--unit", "oz",
            "--description", "created through the CLI process");

        Assert.True(cli.ExitCode == 0, cli.StandardError);
        Assert.Empty(cli.StandardError);
        Assert.StartsWith("ing-", cli.StandardOutput.Trim(), StringComparison.Ordinal);

        await using DesktopHost host = await DesktopHost.OpenAsync(
            DesktopOptions.Create(store.Database, "anonymous"),
            TestContext.Current.CancellationToken);
        MixologySession session = host.Services.GetRequiredService<MixologySessionFactory>()
            .Create(Actor.Anonymous);
        Func<IDesktopWorkspace> factory = IngredientsViewModel.CreateFactory(
            host.Services.GetRequiredService<IngredientsModule>(),
            host.Services.GetRequiredService<IngredientActionProjector>(),
            host.Services.GetRequiredService<TaggedMutationCoordinator>(),
            session,
            Actor.Anonymous);
        await using IngredientsViewModel workspace = Assert.IsType<IngredientsViewModel>(factory());

        await workspace.ActivateAsync(TestContext.Current.CancellationToken);

        Assert.Null(workspace.Error);
        Assert.Contains(workspace.Items, static item => item.Name == "Fresh Desktop Vermouth");
    }

    private static async Task<ProcessResult> RunCliAsync(params string[] arguments)
    {
        ProcessStartInfo start = new("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.ArgumentList.Add(typeof(CliApplication).Assembly.Location);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = start };
        if (!process.Start())
        {
            throw new InvalidOperationException("failed to start the CLI process");
        }

        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new ProcessResult(process.ExitCode, await output, await error);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class TemporaryStore : IAsyncDisposable
    {
        private readonly string root;

        public TemporaryStore(string scope)
        {
            root = Path.Combine(
                Path.GetTempPath(),
                $"mixology-desktop-{scope}",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Database = Path.Combine(root, "mixology.db");
            CliLog = Path.Combine(root, "mixology-cli.log");
        }

        public string Database { get; }
        public string CliLog { get; }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
