using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Cli;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Presentation;
using Mixology.Presentation.Mutations;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces;
using Xunit;

namespace Mixology.Tui.Tests;

public sealed class TuiCrossSurfaceTests
{
    public static TheoryData<string, string[]> AuthorizedRouteCases => new()
    {
        {
            "owner",
            ["dashboard", "drinks", "ingredients", "inventory", "menus", "orders", "audit", "tags"]
        },
        {
            "manager",
            ["dashboard", "drinks", "ingredients", "inventory", "menus", "orders"]
        },
        {
            "anonymous",
            ["dashboard", "drinks", "ingredients", "inventory", "menus"]
        },
    };

    [Theory]
    [MemberData(nameof(AuthorizedRouteCases))]
    public async Task ProductionHostAdvertisesOnlyAuthorizedMountableWorkspaces(
        string actor,
        string[] expectedRoutes)
    {
        await using TemporaryStore store = new("routes");
        MountingRunner runner = new();

        await new HostedTuiRuntime(runner).RunAsync(store.Options(actor));

        Assert.Equal(expectedRoutes, runner.Routes.Select(static route => route.Value));
        Assert.Equal(expectedRoutes, runner.Mounted.Select(static route => route.Value));
        if (!string.Equals(actor, "owner", StringComparison.Ordinal))
        {
            Assert.DoesNotContain(TuiRoutes.Audit.Id, runner.Routes);
        }
    }

    [Fact]
    public async Task TuiWorkspaceMutationIsObservedByFreshCliProcess()
    {
        await using TemporaryStore store = new("tui-to-cli");
        await using (TuiHost host = await TuiHost.OpenAsync(store.Options("manager")))
        {
            MixologySession session = host.Services
                .GetRequiredService<MixologySessionFactory>()
                .Create(Actor.Manager);
            Func<ITuiWorkspace> factory = IngredientsWorkspace.CreateFactory(
                host.Services.GetRequiredService<IngredientsModule>(),
                host.Services.GetRequiredService<IngredientActionProjector>(),
                host.Services.GetRequiredService<TaggedMutationCoordinator>(),
                session,
                Actor.Manager);
            await using IngredientsWorkspace workspace = Assert.IsType<IngredientsWorkspace>(factory());
            await workspace.ActivateAsync();
            await workspace.DrainAsync();

            Assert.True(workspace.Handle('c'));
            workspace.SetField("Name", "Cross Surface Gin");
            workspace.SetField("Category", "spirit");
            workspace.SetField("Unit", "oz");
            workspace.SetField("Description", "created through the TUI workspace");
            Assert.True(workspace.Handle(IngredientsWorkspace.SubmitKey));
            await workspace.DrainAsync();

            Assert.Null(workspace.Status);
            Assert.Contains(workspace.Rows, static item => item.Name == "Cross Surface Gin");
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
        Assert.Equal("Cross Surface Gin", ingredient.GetProperty("name").GetString());
        Assert.Equal("spirit", ingredient.GetProperty("category").GetString());
    }

    [Fact]
    public async Task CliProcessMutationIsObservedByFreshTuiWorkspace()
    {
        await using TemporaryStore store = new("cli-to-tui");
        ProcessResult cli = await RunCliAsync(
            "--db", store.Database,
            "--actor", "manager",
            "--log-level", "error",
            "--log-file", store.CliLog,
            "ingredients", "create", "Fresh Process Vermouth",
            "--category", "other",
            "--unit", "oz",
            "--description", "created through the CLI process");

        Assert.True(cli.ExitCode == 0, cli.StandardError);
        Assert.Empty(cli.StandardError);
        Assert.StartsWith("ing-", cli.StandardOutput.Trim(), StringComparison.Ordinal);

        await using TuiHost host = await TuiHost.OpenAsync(store.Options("anonymous"));
        MixologySession session = host.Services
            .GetRequiredService<MixologySessionFactory>()
            .Create(Actor.Anonymous);
        Func<ITuiWorkspace> factory = IngredientsWorkspace.CreateFactory(
            host.Services.GetRequiredService<IngredientsModule>(),
            host.Services.GetRequiredService<IngredientActionProjector>(),
            host.Services.GetRequiredService<TaggedMutationCoordinator>(),
            session,
            Actor.Anonymous);
        await using IngredientsWorkspace workspace = Assert.IsType<IngredientsWorkspace>(factory());

        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        Assert.Null(workspace.Status);
        Assert.Contains(workspace.Rows, static item => item.Name == "Fresh Process Vermouth");
        Assert.Contains(
            "Fresh Process Vermouth",
            workspace.Render(new Viewport(80, 21)),
            StringComparison.Ordinal);
    }

    private static async Task<ProcessResult> RunCliAsync(params string[] arguments)
    {
        string assembly = typeof(CliApplication).Assembly.Location;
        ProcessStartInfo start = new("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.ArgumentList.Add(assembly);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = start };
        if (!process.Start())
        {
            throw new InvalidOperationException("failed to start the CLI process");
        }

        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
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

    private sealed class MountingRunner : ITuiRunner
    {
        public List<WorkspaceId> Routes { get; } = [];
        public List<WorkspaceId> Mounted { get; } = [];

        public async Task RunAsync(TuiShell shell, CancellationToken cancellationToken = default)
        {
            Routes.AddRange(shell.Routes.Select(static route => route.Id));
            Mounted.Add(shell.CurrentRoute.Id);
            foreach (TuiRoute route in shell.Routes.Where(static route => route.Id != TuiRoutes.Dashboard.Id))
            {
                Assert.True(await shell.NavigateAsync(route.Id, cancellationToken));
                Assert.Equal(route.Id, shell.CurrentRoute.Id);
                Assert.Contains(
                    $"Mixology > {route.Label}",
                    shell.Render(new Viewport(100, 40)),
                    StringComparison.Ordinal);
                Mounted.Add(route.Id);
            }
        }
    }

    private sealed class TemporaryStore : IAsyncDisposable
    {
        private readonly string root;

        public TemporaryStore(string scope)
        {
            root = Path.Combine(
                Path.GetTempPath(),
                $"mixology-tui-{scope}",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Database = Path.Combine(root, "mixology.db");
            CliLog = Path.Combine(root, "mixology-cli.log");
        }

        public string Database { get; }
        public string CliLog { get; }

        public TuiOptions Options(string actor) => TuiOptions.Create(
            Database,
            actor,
            "error",
            "text",
            Path.Combine(root, $"mixology-tui-{actor}.log"),
            metrics: false);

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
