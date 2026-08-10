using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Kernel.Errors;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Inventory;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
using Mixology.Modules.Tagging;
using Xunit;

namespace Mixology.Persistence.Tests;

public sealed class MixologyStoreTests
{
    [Fact]
    public async Task InitializeCreatesAndMigratesOneDeviceLocalDatabase()
    {
        await using StoreFixture fixture = new();

        await fixture.Store.InitializeAsync();
        await fixture.Store.InitializeAsync();

        Assert.True(File.Exists(fixture.DatabasePath));
        await using StoreSession session = await fixture.Store.OpenSessionAsync();
        Assert.Equal(1L, await ScalarAsync<long>(session.Context, "SELECT COUNT(*) FROM store_metadata"));
        string[] discovered = session.Context.Database.GetMigrations().ToArray();
        string[] applied = (await session.Context.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.NotEmpty(discovered);
        Assert.Equal(discovered, applied);
        Assert.Equal("wal", await ScalarAsync<string>(session.Context, "PRAGMA journal_mode"));
        Assert.Equal(1L, await ScalarAsync<long>(session.Context, "PRAGMA foreign_keys"));
    }

    [Fact]
    public async Task CommitAndRollbackControlTheWholeSessionTransaction()
    {
        await using StoreFixture fixture = new();
        await fixture.Store.InitializeAsync();

        await using (StoreSession setup = await fixture.Store.OpenSessionAsync())
        {
            await setup.Context.Database.ExecuteSqlRawAsync(
                "CREATE TABLE transaction_probe (id INTEGER PRIMARY KEY, value TEXT NOT NULL)");
        }

        await using (StoreSession committed = await fixture.Store.OpenSessionAsync())
        {
            await committed.BeginWriteAsync();
            await committed.Context.Database.ExecuteSqlRawAsync(
                "INSERT INTO transaction_probe (id, value) VALUES (1, 'committed')");
            await committed.CommitAsync();
        }

        await using (StoreSession rolledBack = await fixture.Store.OpenSessionAsync())
        {
            await rolledBack.BeginWriteAsync();
            await rolledBack.Context.Database.ExecuteSqlRawAsync(
                "INSERT INTO transaction_probe (id, value) VALUES (2, 'rolled back')");
            await rolledBack.RollbackAsync();
        }

        await using StoreSession verification = await fixture.Store.OpenSessionAsync();
        Assert.Equal(1L, await ScalarAsync<long>(verification.Context, "SELECT COUNT(*) FROM transaction_probe"));
    }

    [Fact]
    public async Task DisposingAnActiveSessionRollsBack()
    {
        await using StoreFixture fixture = new();
        await fixture.Store.InitializeAsync();

        await using (StoreSession setup = await fixture.Store.OpenSessionAsync())
        {
            await setup.Context.Database.ExecuteSqlRawAsync(
                "CREATE TABLE dispose_probe (id INTEGER PRIMARY KEY)");
        }

        await using (StoreSession abandoned = await fixture.Store.OpenSessionAsync())
        {
            await abandoned.BeginWriteAsync();
            await abandoned.Context.Database.ExecuteSqlRawAsync("INSERT INTO dispose_probe (id) VALUES (1)");
        }

        await using StoreSession verification = await fixture.Store.OpenSessionAsync();
        Assert.Equal(0L, await ScalarAsync<long>(verification.Context, "SELECT COUNT(*) FROM dispose_probe"));
    }

    [Fact]
    public async Task SessionSerializesAccessToItsDbContext()
    {
        await using StoreFixture fixture = new();
        await fixture.Store.InitializeAsync();
        await using StoreSession session = await fixture.Store.OpenSessionAsync();
        int active = 0;
        int maximum = 0;

        Task<int>[] operations = Enumerable.Range(0, 8).Select(_ => session.SerializedAsync(async (_, cancellationToken) =>
        {
            int current = Interlocked.Increment(ref active);
            maximum = Math.Max(maximum, current);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Decrement(ref active);
            return current;
        })).ToArray();

        await Task.WhenAll(operations);

        Assert.Equal(1, maximum);
    }

    [Fact]
    public async Task CommitRequiresAnOwnedTransaction()
    {
        await using StoreFixture fixture = new();
        await fixture.Store.InitializeAsync();
        await using StoreSession session = await fixture.Store.OpenSessionAsync();

        InternalError error = await Assert.ThrowsAsync<InternalError>(() => session.CommitAsync());

        Assert.Equal(ErrorKind.Internal, error.Kind);
    }

    [Fact]
    public async Task ModelCacheSeparatesDifferentModuleCompositions()
    {
        string root = Path.Combine(Path.GetTempPath(), "mixology-model-cache-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        ServiceCollection partialServices = new();
        partialServices.AddMixologyPersistence(
            Path.Combine(root, "partial.db"),
            typeof(MigrationAssemblyMarker).Assembly);
        partialServices.AddIngredientsModule();
        await using ServiceProvider partial = partialServices.BuildServiceProvider();
        ServiceCollection fullServices = new();
        fullServices.AddMixologyPersistence(
            Path.Combine(root, "full.db"),
            typeof(MigrationAssemblyMarker).Assembly);
        fullServices.AddAuditModule();
        fullServices.AddIngredientsModule();
        fullServices.AddDrinksModule();
        fullServices.AddInventoryModule();
        fullServices.AddMenusModule();
        fullServices.AddOrdersModule();
        fullServices.AddTaggingModule();
        await using ServiceProvider full = fullServices.BuildServiceProvider();

        try
        {
            await using MixologyDbContext partialContext = await partial
                .GetRequiredService<IDbContextFactory<MixologyDbContext>>()
                .CreateDbContextAsync();
            string[] partialEntities = partialContext.Model.GetEntityTypes()
                .Select(static entity => entity.ClrType.Name)
                .ToArray();
            await full.GetRequiredService<MixologyStore>().InitializeAsync();
            await using StoreSession fullSession = await full.GetRequiredService<MixologyStore>().OpenSessionAsync();
            string[] fullEntities = fullSession.Context.Model.GetEntityTypes()
                .Select(static entity => entity.ClrType.Name)
                .ToArray();

            Assert.Contains("IngredientRow", partialEntities);
            Assert.DoesNotContain("OrderRow", partialEntities);
            Assert.Contains("IngredientRow", fullEntities);
            Assert.Contains("OrderRow", fullEntities);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<T> ScalarAsync<T>(MixologyDbContext context, string commandText)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using DbCommand command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = commandText;
            object? value = await command.ExecuteScalarAsync();
            object converted = Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture)
                ?? throw new InvalidOperationException("Scalar query returned null.");
            return (T)converted;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private sealed class StoreFixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        public StoreFixture()
        {
            root = Path.Combine(Path.GetTempPath(), "mixology-tests", Guid.NewGuid().ToString("N"));
            DatabasePath = Path.Combine(root, "nested", "mixology.db");
            ServiceCollection collection = new();
            collection.AddMixologyPersistence(DatabasePath, typeof(MigrationAssemblyMarker).Assembly);
            collection.AddAuditModule();
            collection.AddIngredientsModule();
            collection.AddInventoryModule();
            collection.AddDrinksModule();
            collection.AddMenusModule();
            collection.AddOrdersModule();
            collection.AddTaggingModule();
            services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            Store = services.GetRequiredService<MixologyStore>();
        }

        public string DatabasePath { get; }

        public MixologyStore Store { get; }

        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
