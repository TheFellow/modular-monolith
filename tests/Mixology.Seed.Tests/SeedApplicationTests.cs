using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Dispatcher;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Audit.Requests;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders;
using Mixology.Modules.Tagging;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Seed.Tests;

public sealed partial class SeedApplicationTests
{
    [Fact]
    public async Task SuccessfulProcessKeepsDiagnosticsOffStandardError()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "mixology-seed-process-tests",
            Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "mixology.db");
        Directory.CreateDirectory(root);

        try
        {
            ProcessStartInfo start = new("dotnet")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(typeof(SeedApplication).Assembly.Location);
            start.Environment[SeedApplication.DatabasePathEnvironmentVariable] = database;
            using Process process = Process.Start(start)
                ?? throw new InvalidOperationException("failed to start seed process");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            Assert.Equal(0, process.ExitCode);
            Assert.Equal(string.Empty, await standardError);
            Assert.Contains(
                $"mixology --db \"{database}\" drinks list",
                await standardOutput,
                StringComparison.Ordinal);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EmbeddedDatasetSeedsCanonicalStoreAndRestartIsIdempotent()
    {
        await using SeedFixture fixture = await SeedFixture.CreateAsync();
        StringWriter output = new();
        StringWriter error = new();
        string? previous = Environment.GetEnvironmentVariable(SeedApplication.DatabasePathEnvironmentVariable);
        int exitCode;
        try
        {
            Environment.SetEnvironmentVariable(
                SeedApplication.DatabasePathEnvironmentVariable,
                fixture.DatabasePath);
            exitCode = await SeedApplication.RunAsync(output, error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SeedApplication.DatabasePathEnvironmentVariable, previous);
        }

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Equal(ExpectedOutput(fixture.DatabasePath), NormalizeIds(output.ToString()));

        await fixture.OpenAsync();
        MixologySession owner = fixture.Session(Actor.Owner);
        IngredientsModule ingredients = fixture.Get<IngredientsModule>();
        InventoryModule inventory = fixture.Get<InventoryModule>();
        DrinksModule drinks = fixture.Get<DrinksModule>();
        MenusModule menus = fixture.Get<MenusModule>();
        TaggingModule tagging = fixture.Get<TaggingModule>();
        AuditModule audit = fixture.Get<AuditModule>();

        Assert.Equal(18, await ingredients.CountAsync(owner, new ListIngredientsRequest()));
        Assert.Equal(18, await inventory.CountAsync(owner, new ListInventoryRequest()));
        Assert.Equal(6, await drinks.CountAsync(owner, new ListDrinksRequest()));
        Assert.Equal(1, await menus.CountAsync(owner, new ListMenusRequest(MenuStatus.Published)));

        Page<Ingredient> ingredientPage = await ingredients.ListAsync(
            owner,
            new ListIngredientsRequest(Limit: 100));
        Ingredient tequila = Assert.Single(ingredientPage.Items, value => value.Name == "Tequila Blanco");
        Assert.Equal(["base-spirit", "origin=mexico"], tequila.Tags.Strings());

        Page<InventoryStock> inventoryPage = await inventory.ListAsync(
            owner,
            new ListInventoryRequest(Limit: 100));
        InventoryStock tequilaStock = Assert.Single(
            inventoryPage.Items,
            value => value.IngredientId == tequila.Id);
        Assert.Equal(25d, tequilaStock.OnHand.Value);
        Assert.Equal("$28.00", tequilaStock.UnitCost?.ToString());
        Assert.Equal(["location=back-bar"], tequilaStock.Tags.Strings());

        Page<Drink> drinkPage = await drinks.ListAsync(owner, new ListDrinksRequest(Limit: 100));
        Dictionary<string, string> drinkNames = drinkPage.Items.ToDictionary(
            static drink => drink.Id.Value,
            static drink => drink.Name,
            StringComparer.Ordinal);
        Page<Menu> menuPage = await menus.ListAsync(
            owner,
            new ListMenusRequest(MenuStatus.Published, Limit: 100));
        Menu menu = Assert.Single(menuPage.Items);
        Assert.Equal("Classic Cocktails", menu.Name);
        Assert.Equal(
            ["Margarita", "Daiquiri", "Gin & Tonic", "Old Fashioned", "Negroni", "Mojito"],
            menu.Items.Select(item => drinkNames[item.DrinkId.Value]));
        Assert.Equal(["collection=classics", "service=all-day"], menu.Tags.Strings());

        IReadOnlyList<TagSummary> summary = await tagging.SummaryAsync(owner);
        Assert.Equal(32, summary.Sum(static value => value.Total));
        Assert.Equal(67, await audit.CountAsync(owner, new ListAuditEntriesRequest()));

        await fixture.CloseAsync();
        StringWriter restartOutput = new();
        StringWriter restartError = new();
        int restartExit = await SeedApplication.RunAsync(
            fixture.DatabasePath,
            restartOutput,
            restartError);

        Assert.Equal(0, restartExit);
        Assert.Equal(string.Empty, restartError.ToString());
        Assert.Equal(ExpectedOutput(fixture.DatabasePath), NormalizeIds(restartOutput.ToString()));

        await fixture.OpenAsync();
        owner = fixture.Session(Actor.Owner);
        Assert.Equal(18, await fixture.Get<IngredientsModule>()
            .CountAsync(owner, new ListIngredientsRequest()));
        Assert.Equal(6, await fixture.Get<DrinksModule>()
            .CountAsync(owner, new ListDrinksRequest()));
        Assert.Equal(1, await fixture.Get<MenusModule>()
            .CountAsync(owner, new ListMenusRequest(MenuStatus.Published)));
    }

    [Fact]
    public async Task LaterFailureKeepsEarlierCommandsAndTypedConflict()
    {
        await using SeedFixture fixture = await SeedFixture.CreateAsync();
        await fixture.OpenAsync();
        SeedIngredient first = Ingredient("first", "Duplicate");
        SeedIngredient duplicate = Ingredient("second", "Duplicate");
        SeedDataset dataset = new([first, duplicate], []);

        Exception failure = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await fixture.Get<SeedRunner>().RunAsync(
                dataset,
                new StringWriter(),
                fixture.DatabasePath));

        Assert.True(AppError.IsConflict(failure));
        MixologySession owner = fixture.Session(Actor.Owner);
        Assert.Equal(1, await fixture.Get<IngredientsModule>()
            .CountAsync(owner, new ListIngredientsRequest()));
        Assert.Equal(0, await fixture.Get<InventoryModule>()
            .CountAsync(owner, new ListInventoryRequest()));
        Assert.Equal(2, await fixture.Get<AuditModule>()
            .CountAsync(owner, new ListAuditEntriesRequest()));
    }

    [Fact]
    public async Task UnexpectedStartupFailureDoesNotDiscloseExceptionDetails()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await SeedApplication.RunAsync("\0", output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal($"error: internal error{Environment.NewLine}", error.ToString());
    }

    [Fact]
    public async Task CancellationKeepsItsCrossCuttingClassification()
    {
        await using SeedFixture fixture = await SeedFixture.CreateAsync();
        StringWriter error = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        int exitCode = await SeedApplication.RunAsync(
            fixture.DatabasePath,
            new StringWriter(),
            error,
            cancellation.Token);

        Assert.Equal(1, exitCode);
        Assert.Equal($"error: operation cancelled{Environment.NewLine}", error.ToString());
    }

    [Fact]
    public void EmbeddedDatasetRetainsReferenceOrderAndAssociationCount()
    {
        SeedDataset dataset = SeedDataset.LoadEmbedded();

        Assert.Equal(18, dataset.Ingredients.Count);
        Assert.Equal(6, dataset.Drinks.Count);
        Assert.Equal("tequila", dataset.Ingredients[0].Key);
        Assert.Equal("cherry", dataset.Ingredients[^1].Key);
        Assert.Equal(
            ["Margarita", "Daiquiri", "Gin & Tonic", "Old Fashioned", "Negroni", "Mojito"],
            dataset.Drinks.Select(static value => value.Name));
        int associations = dataset.Ingredients.Sum(value => value.Tags?.Count ?? 0)
            + dataset.Ingredients.Sum(value => value.Stock.Tags?.Count ?? 0)
            + dataset.Drinks.Sum(value => value.Tags?.Count ?? 0)
            + 2;
        Assert.Equal(32, associations);
    }

    private static SeedIngredient Ingredient(string key, string name) => new()
    {
        Key = key,
        Name = name,
        Category = "spirit",
        Unit = "oz",
        Description = string.Empty,
        Stock = new SeedStock { Quantity = 1d, Cost = "$1.00" },
    };

    private static string NormalizeIds(string value) => EntityIdPattern().Replace(value, "<id>");

    private static string ExpectedOutput(string databasePath)
    {
        string[] names =
        [
            "Tequila Blanco",
            "Vodka",
            "London Dry Gin",
            "Bourbon",
            "White Rum",
            "Triple Sec",
            "Campari",
            "Lime Juice",
            "Lemon Juice",
            "Orange Juice",
            "Simple Syrup",
            "Soda Water",
            "Tonic Water",
            "Angostura Bitters",
            "Orange Bitters",
            "Fresh Mint",
            "Lime Wheel",
            "Maraschino Cherry",
        ];
        string[] drinks = ["Margarita", "Daiquiri", "Gin & Tonic", "Old Fashioned", "Negroni", "Mojito"];
        List<string> lines =
        [
            "=== Mixology Seed ===",
            string.Empty,
            "Creating ingredients...",
            .. names.Select(static name => $"  {name}: <id>"),
            "  Created 18 ingredients",
            string.Empty,
            "Setting inventory levels...",
            "  Inventory stocked",
            string.Empty,
            "Creating drinks...",
            .. drinks.Select(static name => $"  {name}: <id>"),
            string.Empty,
            "Creating menu...",
            "  Menu: <id>",
            "  Menu published with 6 drinks",
            string.Empty,
            "=== Seed Complete ===",
            string.Empty,
            "Created:",
            "  - 18 ingredients",
            "  - 6 classic cocktails",
            "  - 1 published menu",
            string.Empty,
            "View the menu with cost analysis:",
            $"  mixology --db \"{databasePath}\" menus show --id <id> --costs --target-margin 0.7",
            string.Empty,
            "List all drinks:",
            $"  mixology --db \"{databasePath}\" drinks list",
            string.Empty,
            "Check inventory:",
            $"  mixology --db \"{databasePath}\" inventory list",
        ];
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    [GeneratedRegex(@"(?:ing|drk|mnu)-[0-9A-Za-z]{27}", RegexOptions.CultureInvariant)]
    private static partial Regex EntityIdPattern();

    private sealed class SeedFixture : IAsyncDisposable
    {
        private readonly string directory;
        private IHost? host;

        private SeedFixture(string directory)
        {
            this.directory = directory;
            DatabasePath = Path.Combine(directory, "mixology.db");
        }

        public string DatabasePath { get; }

        public static Task<SeedFixture> CreateAsync()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "mixology-seed-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Task.FromResult(new SeedFixture(directory));
        }

        public async Task OpenAsync()
        {
            Assert.Null(host);
            HostApplicationBuilder builder = MixologyHost.CreateBuilder([]);
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
            builder.AddMixology(DatabasePath, typeof(MigrationAssemblyMarker).Assembly);
            builder.Services.AddAuditModule();
            builder.Services.AddIngredientsModule();
            builder.Services.AddDrinksModule();
            builder.Services.AddInventoryModule();
            builder.Services.AddMenusModule();
            builder.Services.AddOrdersModule();
            builder.Services.AddTaggingModule();
            builder.Services.AddSingleton<SeedRunner>();
            host = builder.Build();
            await host.Services.GetRequiredService<MixologyStore>().InitializeAsync();
            await host.StartAsync();
        }

        public TService Get<TService>()
            where TService : notnull =>
            (host ?? throw new InvalidOperationException("fixture is not open"))
            .Services.GetRequiredService<TService>();

        public MixologySession Session(Actor actor) => Get<MixologySessionFactory>().Create(actor);

        public async Task CloseAsync()
        {
            if (host is null)
            {
                return;
            }

            await host.StopAsync();
            host.Dispose();
            host = null;
        }

        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}
