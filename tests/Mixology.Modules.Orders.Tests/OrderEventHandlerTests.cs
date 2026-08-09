using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Tags;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Events;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Events;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Handlers;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Persistence;
using Mixology.Modules.Tagging;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Orders.Tests;

public sealed class OrderEventHandlerTests
{
    [Fact]
    public async Task IngredientRetirementBlocksActiveOrdersWithoutRewritingHistoricalUsage()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        IngredientId retired = IngredientId.New();
        IngredientId alreadyBlocked = IngredientId.New();
        IngredientId replacement = IngredientId.New();
        OrderId pending = await fixture.SeedAsync(
            OrderStatus.Pending,
            retired,
            "Original ingredient name",
            2.5d,
            Unit.Ounce);
        OrderId blocked = await fixture.SeedAsync(
            OrderStatus.Blocked,
            retired,
            "Original ingredient name",
            1d,
            Unit.Ounce,
            [alreadyBlocked]);
        OrderId completed = await fixture.SeedAsync(
            OrderStatus.Completed,
            retired,
            "Completed snapshot",
            3d,
            Unit.Ounce);
        OrderId cancelled = await fixture.SeedAsync(
            OrderStatus.Cancelled,
            retired,
            "Cancelled snapshot",
            4d,
            Unit.Ounce);

        DispatchResult result = await fixture.DispatchAsync(new IngredientDeleted(
            Ingredient(retired, "Retired"),
            fixture.Now,
            Ingredient(replacement, "Replacement"),
            0.75d));

        PersistedOrder pendingState = await fixture.ReadAsync(pending);
        PersistedOrder blockedState = await fixture.ReadAsync(blocked);
        PersistedOrder completedState = await fixture.ReadAsync(completed);
        PersistedOrder cancelledState = await fixture.ReadAsync(cancelled);
        Assert.Equal(OrderStatus.Blocked.Value, pendingState.Status);
        Assert.Equal([retired.Value], pendingState.BlockedIngredientIds);
        Assert.Equal(
            new[] { alreadyBlocked.Value, retired.Value }.Order(StringComparer.Ordinal),
            blockedState.BlockedIngredientIds);
        Assert.Equal(OrderStatus.Completed.Value, completedState.Status);
        Assert.Empty(completedState.BlockedIngredientIds);
        Assert.Equal(OrderStatus.Cancelled.Value, cancelledState.Status);
        Assert.Empty(cancelledState.BlockedIngredientIds);
        Assert.Equal(
            new PersistedUsage(retired.Value, "Original ingredient name", 2.5d, Unit.Ounce.Value),
            Assert.Single(pendingState.Usage));
        Assert.Equal(
            new PersistedUsage(retired.Value, "Original ingredient name", 1d, Unit.Ounce.Value),
            Assert.Single(blockedState.Usage));
        Assert.Equal(
            new[] { pending, blocked }.OrderBy(static id => id.Value).Select(static id => id.EntityUid),
            result.Touches);
        Assert.Equal(1, result.EventCount);
    }

    [Fact]
    public async Task ShortageBlocksEveryAffectedActiveOrderAndRestockUnblocksDeterministically()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        IngredientId adjusted = IngredientId.New();
        IngredientId otherBlocker = IngredientId.New();
        IngredientId unrelated = IngredientId.New();
        OrderId pending = await fixture.SeedAsync(
            OrderStatus.Pending,
            adjusted,
            "Adjusted",
            2d,
            Unit.Ounce);
        OrderId multiplyBlocked = await fixture.SeedAsync(
            OrderStatus.Blocked,
            adjusted,
            "Adjusted",
            1d,
            Unit.Ounce,
            [otherBlocker]);
        OrderId completed = await fixture.SeedAsync(
            OrderStatus.Completed,
            adjusted,
            "Completed",
            1d,
            Unit.Ounce);
        OrderId unrelatedPending = await fixture.SeedAsync(
            OrderStatus.Pending,
            unrelated,
            "Unrelated",
            1d,
            Unit.Ounce);

        DispatchResult shortage = await fixture.DispatchAsync(Adjusted(adjusted, shortage: true));

        Assert.Equal(OrderStatus.Blocked.Value, (await fixture.ReadAsync(pending)).Status);
        Assert.Equal([adjusted.Value], (await fixture.ReadAsync(pending)).BlockedIngredientIds);
        Assert.Equal(
            new[] { adjusted.Value, otherBlocker.Value }.Order(StringComparer.Ordinal),
            (await fixture.ReadAsync(multiplyBlocked)).BlockedIngredientIds);
        Assert.Equal(OrderStatus.Completed.Value, (await fixture.ReadAsync(completed)).Status);
        Assert.Equal(OrderStatus.Pending.Value, (await fixture.ReadAsync(unrelatedPending)).Status);
        Assert.Equal(
            new[] { pending, multiplyBlocked }.OrderBy(static id => id.Value).Select(static id => id.EntityUid),
            shortage.Touches);

        DispatchResult restock = await fixture.DispatchAsync(Adjusted(adjusted, shortage: false));

        PersistedOrder pendingRestocked = await fixture.ReadAsync(pending);
        PersistedOrder stillBlocked = await fixture.ReadAsync(multiplyBlocked);
        Assert.Equal(OrderStatus.Pending.Value, pendingRestocked.Status);
        Assert.Empty(pendingRestocked.BlockedIngredientIds);
        Assert.Equal(OrderStatus.Blocked.Value, stillBlocked.Status);
        Assert.Equal([otherBlocker.Value], stillBlocked.BlockedIngredientIds);
        Assert.Equal(
            new[] { pending, multiplyBlocked }.OrderBy(static id => id.Value).Select(static id => id.EntityUid),
            restock.Touches);
        Assert.Equal(1, restock.EventCount);
    }

    [Fact]
    public async Task RepeatedCorrectionsAreIdempotentAndKeepCanonicalBlockedOrdering()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        IngredientId adjusted = IngredientId.New();
        IngredientId first = IngredientId.New();
        IngredientId second = IngredientId.New();
        OrderId order = await fixture.SeedAsync(
            OrderStatus.Blocked,
            adjusted,
            "Adjusted",
            1d,
            Unit.Ounce,
            [second, first]);

        _ = await fixture.DispatchAsync(Adjusted(adjusted, shortage: true));
        _ = await fixture.DispatchAsync(Adjusted(adjusted, shortage: true));
        PersistedOrder shortage = await fixture.ReadAsync(order);

        Assert.Equal(
            new[] { adjusted.Value, first.Value, second.Value }.Order(StringComparer.Ordinal),
            shortage.BlockedIngredientIds);

        _ = await fixture.DispatchAsync(Adjusted(adjusted, shortage: false));
        _ = await fixture.DispatchAsync(Adjusted(adjusted, shortage: false));
        PersistedOrder corrected = await fixture.ReadAsync(order);

        Assert.Equal(OrderStatus.Blocked.Value, corrected.Status);
        Assert.Equal(new[] { first.Value, second.Value }.Order(StringComparer.Ordinal), corrected.BlockedIngredientIds);
    }

    [Fact]
    public async Task PersistenceFailuresAndCancellationRetainTypedClassification()
    {
        await using Fixture broken = await Fixture.CreateAsync();
        IngredientId ingredientId = IngredientId.New();
        _ = await broken.SeedAsync(
            OrderStatus.Pending,
            ingredientId,
            "Ingredient",
            1d,
            Unit.Ounce);
        await broken.DropUsageAsync();

        Exception persistence = await broken.DispatchFailureAsync(Adjusted(ingredientId, shortage: true));

        Assert.True(AppError.IsInternal(persistence));
        Assert.False(AppError.IsCancellation(persistence));
        Assert.NotNull(AppError.Find<SqliteException>(persistence));

        await using Fixture cancelled = await Fixture.CreateAsync();
        _ = await cancelled.SeedAsync(
            OrderStatus.Pending,
            ingredientId,
            "Ingredient",
            1d,
            Unit.Ounce);
        using CancellationTokenSource source = new();
        source.Cancel();

        Exception cancellation = await cancelled.DispatchFailureAsync(
            Adjusted(ingredientId, shortage: true),
            source.Token);

        Assert.True(AppError.IsCancellation(cancellation));
    }

    private static Ingredient Ingredient(IngredientId id, string name) => new(
        id,
        name,
        IngredientCategory.Other,
        Unit.Ounce,
        string.Empty,
        null,
        TagCollection.Empty);

    private static StockAdjusted Adjusted(IngredientId id, bool shortage) => new(
        new InventoryStock(
            InventoryId.New(),
            id,
            Amount.Create(shortage ? 0d : 10d, Unit.Ounce),
            Amount.Create(1d, Unit.Ounce),
            null,
            DateTimeOffset.UtcNow,
            TagCollection.Empty),
        AdjustmentReason.Corrected.Value,
        shortage);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        private Fixture(string root, ServiceProvider services)
        {
            this.root = root;
            this.services = services;
            Store = services.GetRequiredService<MixologyStore>();
        }

        public DateTimeOffset Now { get; } = new(2026, 8, 10, 5, 0, 0, TimeSpan.Zero);
        private MixologyStore Store { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mixology-order-handler-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ServiceCollection collection = new();
            collection.AddMixologyPersistence(
                Path.Combine(root, "mixology.db"),
                typeof(MigrationAssemblyMarker).Assembly);
            collection.AddAuditModule();
            collection.AddIngredientsModule();
            collection.AddDrinksModule();
            collection.AddInventoryModule();
            collection.AddMenusModule();
            collection.AddOrdersModule();
            collection.AddTaggingModule();
            ServiceProvider services = collection.BuildServiceProvider();
            Fixture fixture = new(root, services);
            await using StoreSession session = await fixture.Store.OpenSessionAsync();
            await session.Context.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async Task<OrderId> SeedAsync(
            OrderStatus status,
            IngredientId ingredientId,
            string ingredientName,
            double quantity,
            Unit unit,
            IReadOnlyList<IngredientId>? blocked = null)
        {
            OrderId id = OrderId.New();
            MenuId menuId = MenuId.New();
            DateTime? completedAt = status == OrderStatus.Completed ? Now.UtcDateTime : null;
            await using StoreSession session = await Store.OpenSessionAsync();
            await session.Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO orders (id, menu_id, status, created_at_utc, completed_at_utc, notes, deleted_at_utc)
                VALUES ({id.Value}, {menuId.Value}, {status.Value}, {Now.UtcDateTime}, {completedAt}, {"snapshot"}, NULL)
                """);
            await session.Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO order_ingredient_usage (order_id, position, ingredient_id, name, quantity, unit)
                VALUES ({id.Value}, {0}, {ingredientId.Value}, {ingredientName}, {quantity}, {unit.Value})
                """);
            foreach (IngredientId blockedId in blocked ?? [])
            {
                await session.Context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO order_blocked_ingredients (order_id, ingredient_id)
                    VALUES ({id.Value}, {blockedId.Value})
                    """);
            }

            return id;
        }

        public async Task<PersistedOrder> ReadAsync(OrderId id)
        {
            await using StoreSession session = await Store.OpenSessionAsync();
            await session.Context.Database.OpenConnectionAsync();
            string status = await ScalarAsync(session, "SELECT status FROM orders WHERE id = $id", id.Value);
            List<string> blocked = await StringsAsync(
                session,
                "SELECT ingredient_id FROM order_blocked_ingredients WHERE order_id = $id ORDER BY ingredient_id",
                id.Value);
            List<PersistedUsage> usage = [];
            await using DbCommand command = session.Context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT ingredient_id, name, quantity, unit
                FROM order_ingredient_usage
                WHERE order_id = $id
                ORDER BY position
                """;
            AddId(command, id.Value);
            await using DbDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                usage.Add(new PersistedUsage(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetDouble(2),
                    reader.GetString(3)));
            }

            return new PersistedOrder(status, blocked, usage);
        }

        public async Task<DispatchResult> DispatchAsync(object domainEvent)
        {
            HandlerDispatcher dispatcher = new();
            await using StoreSession session = await Store.OpenSessionAsync();
            await session.BeginWriteAsync();
            OperationContext operation = new(Actor.Owner, session);
            try
            {
                await new DispatchEventsMiddleware(dispatcher).InvokeAsync(
                    operation,
                    Operation.Command("test.order-event"),
                    context =>
                    {
                        context.AddEvent(domainEvent);
                        return Task.CompletedTask;
                    });
                await session.Context.SaveChangesAsync();
                await session.CommitAsync();
            }
            catch
            {
                await session.RollbackAsync();
                throw;
            }

            return new DispatchResult(operation.TouchedEntities.ToArray(), operation.Events.Count);
        }

        public async Task<Exception> DispatchFailureAsync(
            object domainEvent,
            CancellationToken cancellationToken = default)
        {
            HandlerDispatcher dispatcher = new();
            await using StoreSession session = await Store.OpenSessionAsync(CancellationToken.None);
            await session.BeginWriteAsync(CancellationToken.None);
            OperationContext operation = new(Actor.Owner, session, cancellationToken);
            try
            {
                await new DispatchEventsMiddleware(dispatcher).InvokeAsync(
                    operation,
                    Operation.Command("test.order-event.failure"),
                    context =>
                    {
                        context.AddEvent(domainEvent);
                        return Task.CompletedTask;
                    });
            }
            catch (Exception exception)
            {
                await session.RollbackAsync(CancellationToken.None);
                return exception;
            }

            await session.RollbackAsync(CancellationToken.None);
            throw new InvalidOperationException("Expected event dispatch to fail.");
        }

        public async Task DropUsageAsync()
        {
            await using StoreSession session = await Store.OpenSessionAsync();
            await session.Context.Database.ExecuteSqlRawAsync("DROP TABLE order_ingredient_usage");
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

        private static async Task<string> ScalarAsync(
            StoreSession session,
            string sql,
            string id)
        {
            await using DbCommand command = session.Context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            AddId(command, id);
            return (string)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Expected a scalar result."));
        }

        private static async Task<List<string>> StringsAsync(
            StoreSession session,
            string sql,
            string id)
        {
            await using DbCommand command = session.Context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            AddId(command, id);
            List<string> values = [];
            await using DbDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }

        private static void AddId(DbCommand command, string id)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = "$id";
            parameter.Value = id;
            _ = command.Parameters.Add(parameter);
        }
    }

    private sealed class HandlerDispatcher : IDomainEventDispatcher
    {
        public async Task DispatchAsync(EventHandlerContext context, object domainEvent)
        {
            switch (domainEvent)
            {
                case IngredientDeleted deleted:
                    await new IngredientDeletedHandler().HandleAsync(context, deleted);
                    break;
                case StockAdjusted adjusted:
                    await new StockAdjustedHandler().HandleAsync(context, adjusted);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected event {domainEvent.GetType().Name}.");
            }
        }
    }

    private sealed record DispatchResult(IReadOnlyList<EntityUid> Touches, int EventCount);
    private sealed record PersistedOrder(
        string Status,
        IReadOnlyList<string> BlockedIngredientIds,
        IReadOnlyList<PersistedUsage> Usage);
    private sealed record PersistedUsage(string IngredientId, string Name, double Quantity, string Unit);
}
