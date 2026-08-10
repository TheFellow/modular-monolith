using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Money;
using Mixology.Kernel.Tags;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Events;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Inventory.Events;
using Mixology.Modules.Inventory.Handlers;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Events;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Tagging;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Inventory.Tests;

public sealed class InventoryEventHandlerTests
{
    [Fact]
    public async Task OrderPlacedReservesImmutableUsageWithoutChangingOnHand()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        InventoryStock stock = await fixture.StockAsync(10d, Unit.Ounce);
        Order order = fixture.Order(
            new IngredientUsage(
                stock.IngredientId,
                "Lime",
                Amount.Create(29.5735d, Unit.Milliliter)));

        DispatchResult result = await fixture.DispatchAsync(new OrderPlaced(order));

        InventoryStock reserved = await fixture.GetAsync(stock.IngredientId);
        Assert.Equal(10d, reserved.OnHand.Value);
        Assert.Equal(1d, reserved.Reserved.Value, 5);
        Assert.Equal(9d, reserved.Available.Value, 5);
        Assert.Equal(1, await fixture.ReservationCountAsync(order.Id));
        Assert.Equal([stock.EntityUid], result.Touches);
        Assert.Equal(1, result.EventCount);
    }

    [Fact]
    public async Task OrderPlacedValidatesEveryUsageBeforeAddingAnyReservation()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        InventoryStock enough = await fixture.StockAsync(10d, Unit.Ounce);
        InventoryStock shortStock = await fixture.StockAsync(1d, Unit.Ounce);
        Order order = fixture.Order(
            new IngredientUsage(enough.IngredientId, "Enough", Amount.Create(2d, Unit.Ounce)),
            new IngredientUsage(shortStock.IngredientId, "Short", Amount.Create(2d, Unit.Ounce)));

        Exception error = await fixture.DispatchFailureAsync(new OrderPlaced(order));

        Assert.True(AppError.IsFailedPrecondition(error));
        Assert.Equal(0, await fixture.ReservationCountAsync(order.Id));
        Assert.Equal(10d, (await fixture.GetAsync(enough.IngredientId)).OnHand.Value);
        Assert.Equal(1d, (await fixture.GetAsync(shortStock.IngredientId)).OnHand.Value);
    }

    [Fact]
    public async Task RepeatedOrderPlacedConflictsWithoutDoubleReservation()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        InventoryStock stock = await fixture.StockAsync(5d, Unit.Ounce);
        Order order = fixture.Order(
            new IngredientUsage(stock.IngredientId, "Ingredient", Amount.Create(1d, Unit.Ounce)));
        await fixture.DispatchAsync(new OrderPlaced(order));

        Exception error = await fixture.DispatchFailureAsync(new OrderPlaced(order));

        Assert.True(AppError.IsConflict(error));
        Assert.Equal(1, await fixture.ReservationCountAsync(order.Id));
        Assert.Equal(1d, (await fixture.GetAsync(stock.IngredientId)).Reserved.Value);
    }

    [Fact]
    public async Task OrderCompletedConsumesReservationsAndIsIdempotentAfterRelease()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        InventoryStock used = await fixture.StockAsync(5d, Unit.Ounce);
        InventoryStock unrelated = await fixture.StockAsync(7d, Unit.Ounce);
        Order pending = fixture.Order(
            new IngredientUsage(used.IngredientId, "Used", Amount.Create(2d, Unit.Ounce)));
        await fixture.DispatchAsync(new OrderPlaced(pending));
        Order completed = (pending with
        {
            Status = OrderStatus.Completed,
            CompletedAt = fixture.Now,
        }).Normalize();

        DispatchResult first = await fixture.DispatchAsync(new OrderCompleted(completed));
        DispatchResult repeated = await fixture.DispatchAsync(new OrderCompleted(completed));

        InventoryStock depleted = await fixture.GetAsync(used.IngredientId);
        Assert.Equal(3d, depleted.OnHand.Value, 6);
        Assert.Equal(0d, depleted.Reserved.Value);
        Assert.Equal(fixture.Now, depleted.LastUpdated);
        Assert.Equal(7d, (await fixture.GetAsync(unrelated.IngredientId)).OnHand.Value);
        Assert.Equal(0, await fixture.ReservationCountAsync(pending.Id));
        Assert.Equal([used.EntityUid], first.Touches);
        Assert.Empty(repeated.Touches);
        Assert.Equal(1, first.EventCount);
        Assert.Equal(1, repeated.EventCount);
    }

    [Fact]
    public async Task OrderCompletedPlansEveryConsumptionBeforeMutatingStock()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        InventoryStock first = await fixture.StockAsync(5d, Unit.Ounce);
        InventoryStock incompatible = await fixture.StockAsync(5d, Unit.Ounce);
        Order order = fixture.Order(
            new IngredientUsage(first.IngredientId, "First", Amount.Create(1d, Unit.Ounce)),
            new IngredientUsage(incompatible.IngredientId, "Incompatible", Amount.Create(1d, Unit.Ounce)));
        await fixture.DispatchAsync(new OrderPlaced(order));
        await fixture.SetReservationUnitAsync(order.Id, incompatible.IngredientId, Unit.Piece);
        Order completed = (order with
        {
            Status = OrderStatus.Completed,
            CompletedAt = fixture.Now,
        }).Normalize();

        Exception error = await fixture.DispatchFailureAsync(new OrderCompleted(completed));

        Assert.True(AppError.IsInvalid(error));
        Assert.Equal(5d, await fixture.StockQuantityAsync(first.IngredientId));
        Assert.Equal(5d, await fixture.StockQuantityAsync(incompatible.IngredientId));
        Assert.Equal(2, await fixture.ReservationCountAsync(order.Id));
    }

    [Fact]
    public async Task OrderCancelledReleasesReservationsAndIsIdempotent()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        InventoryStock stock = await fixture.StockAsync(5d, Unit.Ounce);
        Order pending = fixture.Order(
            new IngredientUsage(stock.IngredientId, "Ingredient", Amount.Create(2d, Unit.Ounce)));
        await fixture.DispatchAsync(new OrderPlaced(pending));
        Order cancelled = (pending with { Status = OrderStatus.Cancelled }).Normalize();

        DispatchResult first = await fixture.DispatchAsync(new OrderCancelled(cancelled));
        DispatchResult repeated = await fixture.DispatchAsync(new OrderCancelled(cancelled));

        InventoryStock released = await fixture.GetAsync(stock.IngredientId);
        Assert.Equal(5d, released.OnHand.Value);
        Assert.Equal(0d, released.Reserved.Value);
        Assert.Equal([stock.EntityUid], first.Touches);
        Assert.Empty(repeated.Touches);
        Assert.Equal(0, await fixture.ReservationCountAsync(pending.Id));
        Assert.Equal(1, first.EventCount);
        Assert.Equal(1, repeated.EventCount);
    }

    [Fact]
    public async Task IngredientDeletedRemovesStockAndReservationsAndMissingIsANoOp()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        InventoryStock stock = await fixture.StockAsync(5d, Unit.Ounce);
        Order order = fixture.Order(
            new IngredientUsage(stock.IngredientId, "Retired", Amount.Create(1d, Unit.Ounce)));
        await fixture.DispatchAsync(new OrderPlaced(order));
        Ingredient retired = fixture.Ingredient(stock.IngredientId, "Retired") with
        {
            DeletedAt = fixture.Now,
        };

        DispatchResult first = await fixture.DispatchAsync(
            new IngredientDeleted(retired, fixture.Now, null, 0d));
        DispatchResult repeated = await fixture.DispatchAsync(
            new IngredientDeleted(retired, fixture.Now, null, 0d));
        DispatchResult cancelAfterRetirement = await fixture.DispatchAsync(
            new OrderCancelled((order with { Status = OrderStatus.Cancelled }).Normalize()));

        await Assert.ThrowsAsync<NotFoundError>(() => fixture.GetAsync(stock.IngredientId));
        Assert.Equal(0, await fixture.ReservationCountAsync(order.Id));
        Assert.Equal([stock.EntityUid], first.Touches);
        Assert.Empty(repeated.Touches);
        Assert.Empty(cancelAfterRetirement.Touches);
        Assert.Equal(1, first.EventCount);
    }

    [Fact]
    public async Task CancellationRemainsClassifiableAndDoesNotMutateReservations()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        InventoryStock stock = await fixture.StockAsync(5d, Unit.Ounce);
        Order order = fixture.Order(
            new IngredientUsage(stock.IngredientId, "Ingredient", Amount.Create(1d, Unit.Ounce)));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Exception error = await fixture.DispatchFailureAsync(
            new OrderPlaced(order),
            cancellation.Token);

        Assert.True(AppError.IsCancellation(error));
        Assert.Equal(0, await fixture.ReservationCountAsync(order.Id));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        private Fixture(string root, ServiceProvider services)
        {
            this.root = root;
            this.services = services;
            Store = services.GetRequiredService<MixologyStore>();
            Inventory = services.GetRequiredService<InventoryModule>();
            Manager = services.GetRequiredService<MixologySessionFactory>().Create(Actor.Manager);
        }

        public DateTimeOffset Now { get; } = new(2026, 8, 9, 23, 0, 0, TimeSpan.Zero);
        public MixologyStore Store { get; }
        public InventoryModule Inventory { get; }
        public MixologySession Manager { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mixology-inventory-event-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            DateTimeOffset now = new(2026, 8, 9, 23, 0, 0, TimeSpan.Zero);
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            collection.AddMixologyPersistence(
                Path.Combine(root, "mixology.db"),
                typeof(MigrationAssemblyMarker).Assembly);
            collection.AddMixologyApplication();
            collection.AddAuditModule();
            collection.AddIngredientsModule();
            collection.AddDrinksModule();
            collection.AddInventoryModule();
            collection.AddMenusModule();
            collection.AddOrdersModule();
            collection.AddTaggingModule();
            ServiceProvider services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            Fixture fixture = new(root, services);
            await using StoreSession storeSession = await fixture.Store.OpenSessionAsync();
            await storeSession.Context.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async Task<InventoryStock> StockAsync(double quantity, Unit unit)
        {
            IngredientId ingredientId = IngredientId.New();
            return await Inventory.SetAsync(
                Manager,
                new SetInventoryRequest(
                    ingredientId,
                    Amount.Create(quantity, unit),
                    new Price(1m, Currency.Usd)));
        }

        public Task<InventoryStock> GetAsync(IngredientId ingredientId) =>
            Inventory.GetAsync(Manager, ingredientId);

        public Order Order(params IngredientUsage[] usage) => new Order(
            OrderId.New(),
            MenuId.New(),
            [new OrderItem(DrinkId.New(), 1, string.Empty)],
            usage,
            [],
            OrderStatus.Pending,
            Now,
            null,
            string.Empty,
            null,
            TagCollection.Empty).Normalize();

        public Ingredient Ingredient(IngredientId id, string name) => new(
            id,
            name,
            IngredientCategory.Other,
            Unit.Ounce,
            string.Empty,
            null,
            TagCollection.Empty);

        public async Task<int> ReservationCountAsync(OrderId orderId)
        {
            await using StoreSession storeSession = await Store.OpenSessionAsync();
            return await storeSession.Context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value
                FROM inventory_reservations
                WHERE order_id = {orderId.Value}
                """).SingleAsync();
        }

        public async Task<double> StockQuantityAsync(IngredientId ingredientId)
        {
            await using StoreSession storeSession = await Store.OpenSessionAsync();
            return await storeSession.Context.Database.SqlQuery<double>($"""
                SELECT quantity AS Value
                FROM inventory_stock
                WHERE ingredient_id = {ingredientId.Value}
                """).SingleAsync();
        }

        public async Task SetReservationUnitAsync(
            OrderId orderId,
            IngredientId ingredientId,
            Unit unit)
        {
            await using StoreSession storeSession = await Store.OpenSessionAsync();
            await storeSession.Context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory_reservations
                SET unit = {unit.Value}
                WHERE order_id = {orderId.Value} AND ingredient_id = {ingredientId.Value}
                """);
        }

        public Task<DispatchResult> DispatchAsync(object domainEvent) =>
            DispatchCoreAsync(domainEvent, capture: false, CancellationToken.None);

        public async Task<Exception> DispatchFailureAsync(
            object domainEvent,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _ = await DispatchCoreAsync(domainEvent, capture: true, cancellationToken);
            }
            catch (CapturedException captured)
            {
                return captured.Error;
            }

            throw new InvalidOperationException("Expected event dispatch to fail.");
        }

        private async Task<DispatchResult> DispatchCoreAsync(
            object domainEvent,
            bool capture,
            CancellationToken cancellationToken)
        {
            HandlerDispatcher dispatcher = new(services.GetRequiredService<TimeProvider>());
            await using StoreSession storeSession = await Store.OpenSessionAsync(CancellationToken.None);
            await storeSession.BeginWriteAsync(CancellationToken.None);
            OperationContext operation = new(Actor.Owner, storeSession, cancellationToken);
            try
            {
                await new DispatchEventsMiddleware(dispatcher).InvokeAsync(
                    operation,
                    Operation.Command("test.inventory-event"),
                    context =>
                    {
                        context.AddEvent(domainEvent);
                        return Task.CompletedTask;
                    });
                await storeSession.Context.SaveChangesAsync(CancellationToken.None);
                await storeSession.CommitAsync(CancellationToken.None);
            }
            catch (Exception exception) when (capture)
            {
                await storeSession.RollbackAsync(CancellationToken.None);
                throw new CapturedException(exception);
            }
            catch
            {
                await storeSession.RollbackAsync(CancellationToken.None);
                throw;
            }

            return new DispatchResult(operation.TouchedEntities.ToArray(), operation.Events.Count);
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

    private sealed class HandlerDispatcher(TimeProvider timeProvider) : IDomainEventDispatcher
    {
        public Task DispatchAsync(EventHandlerContext context, object domainEvent) => domainEvent switch
        {
            IngredientDeleted deleted => new IngredientDeletedHandler().HandleAsync(context, deleted),
            OrderPlaced placed => new OrderPlacedHandler().HandleAsync(context, placed),
            OrderCompleted completed => new OrderCompletedHandler(timeProvider).HandleAsync(context, completed),
            OrderCancelled cancelled => new OrderCancelledHandler().HandleAsync(context, cancelled),
            _ => throw new InvalidOperationException($"Unexpected event {domainEvent.GetType().Name}."),
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CapturedException(Exception error) : Exception
    {
        public Exception Error { get; } = error;
    }

    private sealed record DispatchResult(IReadOnlyList<EntityUid> Touches, int EventCount);
}
