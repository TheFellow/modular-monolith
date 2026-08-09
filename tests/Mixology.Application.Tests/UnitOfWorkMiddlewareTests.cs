using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application.Authentication;
using Mixology.Application.Operations;
using Mixology.Kernel.Errors;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Inventory;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Application.Tests;

public sealed class UnitOfWorkMiddlewareTests
{
    [Fact]
    public async Task SuccessfulCommandCommits()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        UnitOfWorkMiddleware middleware = new(fixture.Store);

        await middleware.InvokeAsync(
            new OperationContext(Actor.Owner),
            Operation.Command("probe.create"),
            context => InsertAsync(context, "committed"));

        Assert.Equal(1, await fixture.CountAsync());
    }

    [Fact]
    public async Task FailedCommandRollsBack()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        UnitOfWorkMiddleware middleware = new(fixture.Store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(
            new OperationContext(Actor.Owner),
            Operation.Command("probe.create"),
            async context =>
            {
                await InsertAsync(context, "rolled back");
                throw new InvalidOperationException("handler failed");
            }));

        Assert.Equal(0, await fixture.CountAsync());
    }

    [Fact]
    public async Task CallerTransactionRemainsCallerOwned()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        await using StoreSession session = await fixture.Store.OpenSessionAsync();
        await session.BeginWriteAsync();
        UnitOfWorkMiddleware middleware = new(fixture.Store);

        await middleware.InvokeAsync(
            new OperationContext(Actor.Owner, session),
            Operation.Command("probe.create"),
            context => InsertAsync(context, "caller owned"));

        Assert.True(session.HasTransaction);
        await session.RollbackAsync();
        Assert.Equal(0, await fixture.CountAsync());
    }

    [Fact]
    public async Task SuppliedSessionIsNotDisposed()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        await using StoreSession session = await fixture.Store.OpenSessionAsync();
        UnitOfWorkMiddleware middleware = new(fixture.Store);

        await middleware.InvokeAsync(
            new OperationContext(Actor.Owner, session),
            Operation.Command("probe.create"),
            context => InsertAsync(context, "supplied"));

        Assert.False(session.HasTransaction);
        int count = await session.SerializedAsync(
            (database, _) => ScalarAsync<int>(database, "SELECT COUNT(*) FROM operation_probe"));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task QueryDoesNotOpenAWriteSession()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        UnitOfWorkMiddleware middleware = new(fixture.Store);
        bool reached = false;

        await middleware.InvokeAsync(
            new OperationContext(Actor.Anonymous),
            Operation.Query("probe.list"),
            context =>
            {
                reached = true;
                Assert.Null(context.Session);
                return Task.CompletedTask;
            });

        Assert.True(reached);
    }

    [Fact]
    public async Task UniqueConstraintBecomesTypedConflict()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE probe (value TEXT UNIQUE); INSERT INTO probe VALUES ('same');";
        await command.ExecuteNonQueryAsync();
        command.CommandText = "INSERT INTO probe VALUES ('same');";
        SqliteException providerError = await Assert.ThrowsAsync<SqliteException>(
            () => command.ExecuteNonQueryAsync());

        ConflictError error = Assert.IsType<ConflictError>(PersistenceErrors.TranslateSave(
            new DbUpdateException("save failed", providerError),
            "persist probe.create"));

        Assert.Contains("unique value", error.Message, StringComparison.Ordinal);
        Assert.IsType<DbUpdateException>(error.InnerException);
    }

    private static Task<int> InsertAsync(OperationContext context, string value) =>
        context.Session?.Context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO operation_probe (value) VALUES ({value})",
            context.CancellationToken)
        ?? throw new InvalidOperationException("Command did not receive a store session.");

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

    internal sealed class StoreFixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        private StoreFixture(string root, ServiceProvider services)
        {
            this.root = root;
            this.services = services;
            Store = services.GetRequiredService<MixologyStore>();
            Services = services;
        }

        public MixologyStore Store { get; }

        public IServiceProvider Services { get; }

        public static async Task<StoreFixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-application-tests", Guid.NewGuid().ToString("N"));
            string databasePath = Path.Combine(root, "mixology.db");
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddMixologyPersistence(databasePath, typeof(MigrationAssemblyMarker).Assembly);
            collection.AddMixologyApplication();
            collection.AddAuditModule();
            collection.AddIngredientsModule();
            collection.AddInventoryModule();
            ServiceProvider services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            StoreFixture fixture = new(root, services);
            await fixture.Store.InitializeAsync();
            await using StoreSession session = await fixture.Store.OpenSessionAsync();
            await session.Context.Database.ExecuteSqlRawAsync(
                "CREATE TABLE operation_probe (id INTEGER PRIMARY KEY, value TEXT NOT NULL)");
            return fixture;
        }

        public async Task<int> CountAsync()
        {
            await using StoreSession session = await Store.OpenSessionAsync();
            return await ScalarAsync<int>(session.Context, "SELECT COUNT(*) FROM operation_probe");
        }

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
