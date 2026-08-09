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
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders.Events;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Requests;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Orders.Tests;

public sealed class OrdersModuleTests
{
    [Fact]
    public async Task PlaceCapturesAggregatedIngredientUsageAsAnImmutableSnapshot()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        (Ingredient ingredient, Drink drink, Menu menu) = await fixture.CreatePublishedDrinkAsync(
            "Gin",
            500d,
            50d);

        Order placed = await fixture.Orders.PlaceAsync(
            fixture.Session(Actor.Bartender),
            new PlaceOrderRequest(
                menu.Id,
                [new PlaceOrderItem(drink.Id, 2, "  up  ")],
                "  table seven  "));
        await fixture.Ingredients.UpdateAsync(
            fixture.Manager,
            new UpdateIngredientRequest(ingredient.Id, Name: "London Dry Gin"));
        Order loaded = await fixture.Orders.GetAsync(fixture.Session(Actor.Sommelier), placed.Id);

        IngredientUsage usage = Assert.Single(loaded.IngredientUsage);
        Assert.Equal(ingredient.Id, usage.IngredientId);
        Assert.Equal("Gin", usage.Name);
        Assert.Equal(100d, usage.Amount.Value, 6);
        Assert.Equal("up", Assert.Single(loaded.Items).Notes);
        Assert.Equal("table seven", loaded.Notes);
        Assert.Equal(OrderStatus.Pending, loaded.Status);
        Assert.IsType<OrderPlaced>(fixture.Events.Events.Single(static value => value is OrderPlaced));
    }

    [Fact]
    public async Task CompleteAndCancelPreserveExactTransitionAndIdempotencyRules()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        (_, Drink drink, Menu menu) = await fixture.CreatePublishedDrinkAsync("Rum", 500d, 30d);
        Order completedOrder = await fixture.PlaceAsync(menu.Id, drink.Id);
        Order cancelledOrder = await fixture.PlaceAsync(menu.Id, drink.Id);

        fixture.Events.Events.Clear();
        Order completed = await fixture.Orders.CompleteAsync(fixture.Manager, completedOrder.Id);
        Order completedAgain = await fixture.Orders.CompleteAsync(fixture.Manager, completedOrder.Id);
        Assert.Equal(OrderStatus.Completed, completed.Status);
        Assert.Equal(fixture.Now, completed.CompletedAt);
        Assert.Equal(completed.Id, completedAgain.Id);
        Assert.Equal(completed.Status, completedAgain.Status);
        Assert.Equal(completed.CompletedAt, completedAgain.CompletedAt);
        Assert.Single(fixture.Events.Events, static value => value is OrderCompleted);
        await Assert.ThrowsAsync<InvalidError>(() => fixture.Orders.CancelAsync(
            fixture.Manager,
            completedOrder.Id));

        fixture.Events.Events.Clear();
        Order cancelled = await fixture.Orders.CancelAsync(fixture.Session(Actor.Bartender), cancelledOrder.Id);
        Order cancelledAgain = await fixture.Orders.CancelAsync(
            fixture.Session(Actor.Bartender),
            cancelledOrder.Id);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.CompletedAt);
        Assert.Equal(cancelled.Id, cancelledAgain.Id);
        Assert.Equal(cancelled.Status, cancelledAgain.Status);
        Assert.Single(fixture.Events.Events, static value => value is OrderCancelled);
        await Assert.ThrowsAsync<InvalidError>(() => fixture.Orders.CompleteAsync(
            fixture.Manager,
            cancelledOrder.Id));
    }

    [Fact]
    public async Task BlockedCannotCompleteButCanCancelAndKeepsBlockingSnapshot()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        (Ingredient ingredient, Drink drink, Menu menu) = await fixture.CreatePublishedDrinkAsync(
            "Vermouth",
            500d,
            10d);
        Order order = await fixture.PlaceAsync(menu.Id, drink.Id);
        await fixture.BlockAsync(order.Id, ingredient.Id);

        InvalidError error = await Assert.ThrowsAsync<InvalidError>(() => fixture.Orders.CompleteAsync(
            fixture.Manager,
            order.Id));
        Order cancelled = await fixture.Orders.CancelAsync(fixture.Manager, order.Id);

        Assert.Contains("blocked", error.Message, StringComparison.Ordinal);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal([ingredient.Id], cancelled.BlockedIngredientIds);
    }

    [Fact]
    public async Task PlacementRejectsUnpublishedMissingMenuDrinksAndInsufficientStock()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.CreateIngredientAsync("Lime");
        Drink drink = await fixture.CreateDrinkAsync("Daiquiri", ingredient.Id, 2d);
        Menu draft = await fixture.Menus.CreateAsync(fixture.Manager, new CreateMenuRequest("Draft"));

        await Assert.ThrowsAsync<FailedPreconditionError>(() => fixture.Orders.PlaceAsync(
            fixture.Manager,
            new PlaceOrderRequest(draft.Id, [new PlaceOrderItem(drink.Id, 1)])));

        await fixture.Inventory.SetAsync(
            fixture.Manager,
            new SetInventoryRequest(
                ingredient.Id,
                Amount.Create(3d, Unit.Milliliter),
                new Price(1m, Currency.Usd)));
        Menu menu = await fixture.Menus.CreateAsync(fixture.Manager, new CreateMenuRequest("Published"));
        menu = await fixture.Menus.AddDrinkAsync(
            fixture.Manager,
            new AddMenuItemRequest(menu.Id, drink.Id));
        menu = await fixture.Menus.PublishAsync(fixture.Manager, menu.Id);
        await fixture.Inventory.SetAsync(
            fixture.Manager,
            new SetInventoryRequest(
                ingredient.Id,
                Amount.Create(1d, Unit.Milliliter),
                new Price(1m, Currency.Usd)));

        await Assert.ThrowsAsync<InvalidError>(() => fixture.Orders.PlaceAsync(
            fixture.Manager,
            new PlaceOrderRequest(menu.Id, [new PlaceOrderItem(drink.Id, 1)])));

        Ingredient other = await fixture.CreateIngredientAsync("Soda");
        Drink absent = await fixture.CreateDrinkAsync("Highball", other.Id, 1d);
        await Assert.ThrowsAsync<NotFoundError>(() => fixture.Orders.PlaceAsync(
            fixture.Manager,
            new PlaceOrderRequest(menu.Id, [new PlaceOrderItem(absent.Id, 1)])));
    }

    [Fact]
    public async Task ListCountPagingFiltersAndCommandAuthorizationUseProductionPipeline()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        (_, Drink drink, Menu menu) = await fixture.CreatePublishedDrinkAsync("Tequila", 500d, 25d);
        Order pending = await fixture.PlaceAsync(menu.Id, drink.Id);
        Order completed = await fixture.PlaceAsync(menu.Id, drink.Id);
        await fixture.Orders.CompleteAsync(fixture.Manager, completed.Id);

        Mixology.Kernel.Paging.Page<Order> first = await fixture.Orders.ListAsync(
            fixture.Session(Actor.Sommelier),
            new ListOrdersRequest(MenuId: menu.Id, Filter: "status in [\"pending\", \"completed\"]", Limit: 1));
        Mixology.Kernel.Paging.Page<Order> second = await fixture.Orders.ListAsync(
            fixture.Session(Actor.Sommelier),
            new ListOrdersRequest(MenuId: menu.Id, Cursor: first.Next, Limit: 1));

        Assert.Single(first.Items);
        Assert.Single(second.Items);
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        Assert.Equal(1, await fixture.Orders.CountAsync(
            fixture.Session(Actor.Sommelier),
            new ListOrdersRequest(Status: OrderStatus.Pending)));
        Assert.Contains(pending.Id, new[] { first.Items[0].Id, second.Items[0].Id });
        await Assert.ThrowsAsync<PermissionError>(() => fixture.Orders.PlaceAsync(
            fixture.Session(Actor.Sommelier),
            new PlaceOrderRequest(menu.Id, [new PlaceOrderItem(drink.Id, 1)])));
        await Assert.ThrowsAsync<PermissionError>(() => fixture.Orders.GetAsync(
            fixture.Session(Actor.Anonymous),
            pending.Id));
    }

    [Fact]
    public async Task PlacementUsesCatalogRatioForAnExplicitSubstitute()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient primary = await fixture.CreateIngredientAsync("Simple Syrup", Unit.Ounce);
        Ingredient honey = await fixture.CreateIngredientAsync("Honey Syrup", Unit.Ounce);
        await fixture.SetStockAsync(primary.Id, 10d, Unit.Ounce);
        await fixture.SetStockAsync(honey.Id, 3d, Unit.Ounce);
        Drink drink = await fixture.CreateDrinkAsync(
            "Honey Sour",
            [new RecipeIngredient(
                primary.Id,
                Amount.Create(2d, Unit.Ounce),
                substitutes: [honey.Id])]);
        Menu menu = await fixture.CreatePublishedMenuAsync("Substitution Menu", drink.Id);
        await fixture.SetStockAsync(primary.Id, 0d, Unit.Ounce);

        Order order = await fixture.Orders.PlaceAsync(
            fixture.Manager,
            new PlaceOrderRequest(menu.Id, [new PlaceOrderItem(drink.Id, 2)]));

        IngredientUsage usage = Assert.Single(order.IngredientUsage);
        Assert.Equal(honey.Id, usage.IngredientId);
        Assert.Equal("Honey Syrup", usage.Name);
        Assert.Equal(3d, usage.Amount.Value, 6);
        Assert.Equal(Unit.Ounce, usage.Amount.Unit);
    }

    [Fact]
    public async Task PlacementPrefersHigherQualityCatalogSubstituteOverExplicitFallback()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient bourbon = await fixture.CreateIngredientAsync("Bourbon", Unit.Ounce);
        Ingredient rye = await fixture.CreateIngredientAsync("Rye Whiskey", Unit.Ounce);
        Ingredient scotch = await fixture.CreateIngredientAsync("Scotch", Unit.Ounce);
        await fixture.SetStockAsync(bourbon.Id, 10d, Unit.Ounce);
        await fixture.SetStockAsync(rye.Id, 5d, Unit.Ounce);
        await fixture.SetStockAsync(scotch.Id, 10d, Unit.Ounce);
        Drink drink = await fixture.CreateDrinkAsync(
            "Whiskey Cocktail",
            [new RecipeIngredient(
                bourbon.Id,
                Amount.Create(1d, Unit.Ounce),
                substitutes: [scotch.Id])]);
        Menu menu = await fixture.CreatePublishedMenuAsync("Quality Menu", drink.Id);
        await fixture.SetStockAsync(bourbon.Id, 0d, Unit.Ounce);

        Order order = await fixture.Orders.PlaceAsync(
            fixture.Manager,
            new PlaceOrderRequest(menu.Id, [new PlaceOrderItem(drink.Id, 2)]));

        IngredientUsage usage = Assert.Single(order.IngredientUsage);
        Assert.Equal(rye.Id, usage.IngredientId);
        Assert.Equal(2d, usage.Amount.Value, 6);
    }

    [Fact]
    public async Task PlacementBacktracksWhenPreferredSubstituteStockIsShared()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient first = await fixture.CreateIngredientAsync("Reservation First", Unit.Ounce);
        Ingredient second = await fixture.CreateIngredientAsync("Reservation Second", Unit.Ounce);
        Ingredient shared = await fixture.CreateIngredientAsync("Reservation Shared", Unit.Ounce);
        Ingredient fallback = await fixture.CreateIngredientAsync("Reservation Fallback", Unit.Ounce);
        await fixture.SetStockAsync(first.Id, 10d, Unit.Ounce);
        await fixture.SetStockAsync(second.Id, 10d, Unit.Ounce);
        await fixture.SetStockAsync(shared.Id, 1.5d, Unit.Ounce);
        await fixture.SetStockAsync(fallback.Id, 1d, Unit.Ounce);
        Drink drink = await fixture.CreateDrinkAsync(
            "Reservation Cocktail",
            [
                new RecipeIngredient(
                    first.Id,
                    Amount.Create(1d, Unit.Ounce),
                    substitutes: [shared.Id]),
                new RecipeIngredient(
                    second.Id,
                    Amount.Create(1d, Unit.Ounce),
                    substitutes: [shared.Id, fallback.Id]),
            ]);
        Menu menu = await fixture.CreatePublishedMenuAsync("Reservation Menu", drink.Id);
        await fixture.SetStockAsync(first.Id, 0d, Unit.Ounce);
        await fixture.SetStockAsync(second.Id, 0d, Unit.Ounce);

        Order order = await fixture.Orders.PlaceAsync(
            fixture.Manager,
            new PlaceOrderRequest(menu.Id, [new PlaceOrderItem(drink.Id, 1)]));

        Assert.Equal(2, order.IngredientUsage.Count);
        Assert.Contains(order.IngredientUsage, usage =>
            usage.IngredientId == shared.Id && Math.Abs(usage.Amount.Value - 1d) < 0.000001d);
        Assert.Contains(order.IngredientUsage, usage =>
            usage.IngredientId == fallback.Id && Math.Abs(usage.Amount.Value - 1d) < 0.000001d);
    }

    [Fact]
    public async Task PlacementPreservesCancellationAndTypedDependencyErrors()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        (_, Drink drink, Menu menu) = await fixture.CreatePublishedDrinkAsync("Cancellation", 10d, 1d);
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();

        Exception cancellation = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Orders.PlaceAsync(
                fixture.Manager,
                new PlaceOrderRequest(menu.Id, [new PlaceOrderItem(drink.Id, 1)]),
                cancelled.Token));
        Assert.True(AppError.IsCancellation(new InvalidOperationException("wrapped", cancellation)));

        await fixture.DropInventoryAsync();
        Exception dependency = await Assert.ThrowsAsync<InternalError>(() => fixture.Orders.PlaceAsync(
            fixture.Manager,
            new PlaceOrderRequest(menu.Id, [new PlaceOrderItem(drink.Id, 1)])));
        Assert.True(AppError.IsInternal(new AggregateException(new IOException("other"), dependency)));
        Assert.False(AppError.IsCancellation(dependency));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        private Fixture(string root, ServiceProvider services, CapturingEvents events, DateTimeOffset now)
        {
            this.root = root;
            this.services = services;
            Events = events;
            Now = now;
            Manager = Session(Actor.Manager);
            Ingredients = services.GetRequiredService<IngredientsModule>();
            Inventory = services.GetRequiredService<InventoryModule>();
            Drinks = services.GetRequiredService<DrinksModule>();
            Menus = services.GetRequiredService<MenusModule>();
            Orders = services.GetRequiredService<OrdersModule>();
        }

        public DateTimeOffset Now { get; }
        public MixologySession Manager { get; }
        public CapturingEvents Events { get; }
        public IngredientsModule Ingredients { get; }
        public InventoryModule Inventory { get; }
        public DrinksModule Drinks { get; }
        public MenusModule Menus { get; }
        public OrdersModule Orders { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-orders-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            DateTimeOffset now = new(2026, 8, 9, 23, 0, 0, TimeSpan.Zero);
            CapturingEvents events = new();
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            collection.AddSingleton<IDomainEventDispatcher>(events);
            collection.AddMixologyPersistence(
                Path.Combine(root, "mixology.db"),
                typeof(MigrationAssemblyMarker).Assembly);
            collection.AddMixologyApplication();
            collection.AddAuditModule();
            collection.AddIngredientsModule();
            collection.AddInventoryModule();
            collection.AddDrinksModule();
            collection.AddMenusModule();
            collection.AddOrdersModule();
            ServiceProvider services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            await services.GetRequiredService<MixologyStore>().InitializeAsync();

            return new Fixture(root, services, events, now);
        }

        public MixologySession Session(Actor actor) =>
            services.GetRequiredService<MixologySessionFactory>().Create(actor);

        public async Task<(Ingredient Ingredient, Drink Drink, Menu Menu)> CreatePublishedDrinkAsync(
            string ingredientName,
            double stock,
            double required)
        {
            Ingredient ingredient = await CreateIngredientAsync(ingredientName);
            await Inventory.SetAsync(
                Manager,
                new SetInventoryRequest(
                    ingredient.Id,
                    Amount.Create(stock, Unit.Milliliter),
                    new Price(0.10m, Currency.Usd)));
            Drink drink = await CreateDrinkAsync(ingredientName + " Drink", ingredient.Id, required);
            Menu menu = await Menus.CreateAsync(Manager, new CreateMenuRequest(ingredientName + " Menu"));
            menu = await Menus.AddDrinkAsync(Manager, new AddMenuItemRequest(menu.Id, drink.Id));
            menu = await Menus.PublishAsync(Manager, menu.Id);
            return (ingredient, drink, menu);
        }

        public Task<Ingredient> CreateIngredientAsync(string name, Unit? unit = null) => Ingredients.CreateAsync(
            Manager,
            new CreateIngredientRequest(name, IngredientCategory.Spirit, unit ?? Unit.Milliliter));

        public Task<Drink> CreateDrinkAsync(string name, IngredientId ingredientId, double amount) =>
            CreateDrinkAsync(
                name,
                [new RecipeIngredient(ingredientId, Amount.Create(amount, Unit.Milliliter))]);

        public Task<Drink> CreateDrinkAsync(string name, IReadOnlyList<RecipeIngredient> ingredients) =>
            Drinks.CreateAsync(
                Manager,
                new CreateDrinkRequest(
                    name,
                    DrinkCategory.Cocktail,
                    GlassType.Coupe,
                    new Recipe(ingredients, ["Stir"])));

        public async Task<Menu> CreatePublishedMenuAsync(string name, params DrinkId[] drinkIds)
        {
            Menu menu = await Menus.CreateAsync(Manager, new CreateMenuRequest(name));
            foreach (DrinkId drinkId in drinkIds)
            {
                menu = await Menus.AddDrinkAsync(Manager, new AddMenuItemRequest(menu.Id, drinkId));
            }

            return await Menus.PublishAsync(Manager, menu.Id);
        }

        public Task<Mixology.Modules.Inventory.Models.InventoryStock> SetStockAsync(
            IngredientId ingredientId,
            double quantity,
            Unit unit) =>
            Inventory.SetAsync(
                Manager,
                new SetInventoryRequest(
                    ingredientId,
                    Amount.Create(quantity, unit),
                    new Price(1m, Currency.Usd)));

        public Task<Order> PlaceAsync(MenuId menuId, DrinkId drinkId) => Orders.PlaceAsync(
            Manager,
            new PlaceOrderRequest(menuId, [new PlaceOrderItem(drinkId, 1)]));

        public async Task BlockAsync(OrderId orderId, IngredientId ingredientId)
        {
            await using StoreSession session = await services.GetRequiredService<MixologyStore>().OpenSessionAsync();
            await session.Context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE orders SET status = {"blocked"} WHERE id = {orderId.Value}
                """);
            await session.Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO order_blocked_ingredients (order_id, ingredient_id)
                VALUES ({orderId.Value}, {ingredientId.Value})
                """);
        }

        public async Task DropInventoryAsync()
        {
            await using StoreSession session = await services.GetRequiredService<MixologyStore>().OpenSessionAsync();
            await session.Context.Database.ExecuteSqlRawAsync("DROP TABLE inventory_reservations");
            await session.Context.Database.ExecuteSqlRawAsync("DROP TABLE inventory_stock");
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

    private sealed class CapturingEvents : IDomainEventDispatcher
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
