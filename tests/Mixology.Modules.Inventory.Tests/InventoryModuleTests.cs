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
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Inventory.Events;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Inventory.Tests;

public sealed class InventoryModuleTests
{
    [Fact]
    public async Task SetGetListCountAndAdjustUseTheRealSessionPipeline()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        IngredientId gin = IngredientId.New();
        IngredientId lime = IngredientId.New();
        MixologySession manager = fixture.Session(Actor.Manager);

        InventoryStock first = await fixture.Module.SetAsync(
            manager,
            new SetInventoryRequest(gin, Amount.Create(100d, Unit.Milliliter), new Price(0.25m, Currency.Usd)));
        await fixture.Module.SetAsync(
            manager,
            new SetInventoryRequest(lime, Amount.Create(4d, Unit.Piece), new Price(0.50m, Currency.Usd)));
        InventoryStock adjusted = await fixture.Module.AdjustAsync(
            manager,
            new AdjustInventoryRequest(gin, AdjustmentReason.Used, Amount.Create(-1d, Unit.Centiliter)));
        InventoryStock loaded = await fixture.Module.GetAsync(fixture.Session(Actor.Anonymous), gin);
        Mixology.Kernel.Paging.Page<InventoryStock> low = await fixture.Module.ListAsync(
            fixture.Session(Actor.Anonymous),
            new ListInventoryRequest(LowStock: 5d, Filter: "quantity > 0", Limit: 1));

        Assert.StartsWith("inv-", first.Id.Value, StringComparison.Ordinal);
        Assert.Equal(90d, adjusted.OnHand.Value, 6);
        Assert.Equal(90d, loaded.OnHand.Value, 6);
        Assert.Equal(new Price(0.25m, Currency.Usd), loaded.UnitCost);
        Assert.Single(low.Items);
        Assert.Equal(lime, low.Items[0].IngredientId);
        Assert.Equal(2, await fixture.Module.CountAsync(
            fixture.Session(Actor.Anonymous),
            new ListInventoryRequest(Filter: "quantity > 0")));
        Assert.Equal(3, fixture.Events.Events.Count);
        Assert.Equal("used", Assert.IsType<StockAdjusted>(fixture.Events.Events[^1]).Reason);
    }

    [Fact]
    public async Task ReservationsDriveReservedAvailableAndShortageWithoutChangingOnHand()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        IngredientId ingredientId = IngredientId.New();
        MixologySession manager = fixture.Session(Actor.Manager);
        await fixture.Module.SetAsync(
            manager,
            new SetInventoryRequest(ingredientId, Amount.Create(5d, Unit.Ounce), new Price(1m, Currency.Usd)));
        await fixture.InsertReservationAsync(OrderId.New(), ingredientId, 2d, Unit.Ounce);

        InventoryStock before = await fixture.Module.GetAsync(fixture.Session(Actor.Anonymous), ingredientId);
        InventoryStock after = await fixture.Module.AdjustAsync(
            manager,
            new AdjustInventoryRequest(ingredientId, AdjustmentReason.Spilled, Amount.Create(-10d, Unit.Ounce)));
        StockAdjusted stockAdjusted = Assert.IsType<StockAdjusted>(fixture.Events.Events[^1]);

        Assert.Equal(5d, before.OnHand.Value);
        Assert.Equal(2d, before.Reserved.Value);
        Assert.Equal(3d, before.Available.Value, 6);
        Assert.Equal(0d, after.OnHand.Value);
        Assert.Equal(2d, after.Reserved.Value);
        Assert.Equal(0d, after.Available.Value);
        Assert.True(stockAdjusted.Shortage);
    }

    [Fact]
    public async Task DenialsAndMissingStockRemainPreciselyTyped()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        IngredientId ingredientId = IngredientId.New();
        SetInventoryRequest set = new(
            ingredientId,
            Amount.Create(1d, Unit.Piece),
            new Price(1m, Currency.Usd));

        await Assert.ThrowsAsync<PermissionError>(() => fixture.Module.SetAsync(
            fixture.Session(Actor.Bartender),
            set));
        await Assert.ThrowsAsync<NotFoundError>(() => fixture.Module.GetAsync(
            fixture.Session(Actor.Anonymous),
            ingredientId));
        await Assert.ThrowsAsync<NotFoundError>(() => fixture.Module.AdjustAsync(
            fixture.Session(Actor.Manager),
            new AdjustInventoryRequest(
                ingredientId,
                AdjustmentReason.Corrected,
                UnitCost: new Price(2m, Currency.Usd))));
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        private Fixture(string root, ServiceProvider services, CapturingEvents events)
        {
            this.root = root;
            this.services = services;
            Events = events;
            Module = services.GetRequiredService<InventoryModule>();
        }

        public InventoryModule Module { get; }
        public CapturingEvents Events { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-inventory-tests", Guid.NewGuid().ToString("N"));
            string databasePath = Path.Combine(root, "mixology.db");
            Directory.CreateDirectory(root);
            CapturingEvents events = new();
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddSingleton<TimeProvider>(new FixedTimeProvider(
                new DateTimeOffset(2026, 8, 9, 20, 30, 0, TimeSpan.Zero)));
            collection.AddSingleton<IDomainEventDispatcher>(events);
            collection.AddMixologyPersistence(databasePath, typeof(MigrationAssemblyMarker).Assembly);
            collection.AddMixologyApplication();
            collection.AddAuditModule();
            collection.AddIngredientsModule();
            collection.AddDrinksModule();
            collection.AddMenusModule();
            collection.AddOrdersModule();
            collection.AddInventoryModule();
            ServiceProvider services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            Fixture fixture = new(root, services, events);
            await services.GetRequiredService<MixologyStore>().InitializeAsync();
            return fixture;
        }

        public MixologySession Session(Actor actor) =>
            services.GetRequiredService<MixologySessionFactory>().Create(actor);

        public async Task InsertReservationAsync(
            OrderId orderId,
            IngredientId ingredientId,
            double quantity,
            Unit unit)
        {
            await using StoreSession session = await services.GetRequiredService<MixologyStore>().OpenSessionAsync();
            await session.Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO inventory_reservations (id, order_id, ingredient_id, quantity, unit)
                VALUES ({$"{orderId.Value}:{ingredientId.Value}"}, {orderId.Value}, {ingredientId.Value}, {quantity}, {unit.Value})
                """);
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

    internal sealed class CapturingEvents : IDomainEventDispatcher
    {
        public List<object> Events { get; } = [];

        public Task DispatchAsync(EventHandlerContext context, object domainEvent)
        {
            _ = context;
            Events.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
