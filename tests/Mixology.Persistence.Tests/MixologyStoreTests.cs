using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Kernel.Errors;
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
        Assert.Equal(1L, await ScalarAsync<long>(session.Context, "SELECT COUNT(*) FROM __EFMigrationsHistory"));
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

        AppError error = await Assert.ThrowsAsync<AppError>(() => session.CommitAsync());

        Assert.Equal(ErrorKind.Internal, error.Kind);
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
            collection.AddMixologyPersistence(DatabasePath);
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
