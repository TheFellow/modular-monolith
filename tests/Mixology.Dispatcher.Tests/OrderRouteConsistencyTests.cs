using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Dispatcher;
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
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Requests;
using Mixology.Modules.Tagging;
using Mixology.Persistence;

namespace Mixology.Dispatcher.Tests;

public sealed class OrderRouteConsistencyTests
{
    [Fact]
    public async Task MenuFinalizerObservesReservedReleasedAndConsumedInventoryInsideTransaction()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.Ingredients.CreateAsync(
            fixture.Manager,
            new CreateIngredientRequest("Gin", IngredientCategory.Spirit, Unit.Ounce));
        _ = await fixture.Inventory.SetAsync(
            fixture.Manager,
            new SetInventoryRequest(
                ingredient.Id,
                Amount.Create(3d, Unit.Ounce),
                new Price(1m, Currency.Usd)));
        Drink drink = await fixture.Drinks.CreateAsync(
            fixture.Manager,
            new CreateDrinkRequest(
                "Gin Pour",
                DrinkCategory.Cocktail,
                GlassType.Rocks,
                new Recipe(
                    [new RecipeIngredient(ingredient.Id, Amount.Create(1d, Unit.Ounce))],
                    ["Pour"])));
        Menu menu = await fixture.Menus.CreateAsync(
            fixture.Manager,
            new CreateMenuRequest("Service"));
        menu = await fixture.Menus.AddDrinkAsync(
            fixture.Manager,
            new AddMenuItemRequest(menu.Id, drink.Id));
        menu = await fixture.Menus.PublishAsync(fixture.Manager, menu.Id);
        Assert.Equal(Availability.Available, Assert.Single(menu.Items).Availability);

        Order cancelled = await fixture.Orders.PlaceAsync(
            fixture.Manager,
            new PlaceOrderRequest(menu.Id, [new PlaceOrderItem(drink.Id, 1)]));
        Assert.Equal(
            Availability.Limited,
            Assert.Single((await fixture.Menus.GetAsync(fixture.Manager, menu.Id)).Items).Availability);
        Assert.Equal(1d, (await fixture.Inventory.GetAsync(fixture.Manager, ingredient.Id)).Reserved.Value);

        _ = await fixture.Orders.CancelAsync(fixture.Manager, cancelled.Id);
        Assert.Equal(
            Availability.Available,
            Assert.Single((await fixture.Menus.GetAsync(fixture.Manager, menu.Id)).Items).Availability);
        Assert.Equal(0d, (await fixture.Inventory.GetAsync(fixture.Manager, ingredient.Id)).Reserved.Value);

        Order completed = await fixture.Orders.PlaceAsync(
            fixture.Manager,
            new PlaceOrderRequest(menu.Id, [new PlaceOrderItem(drink.Id, 1)]));
        _ = await fixture.Orders.CompleteAsync(fixture.Manager, completed.Id);

        InventoryStock stock = await fixture.Inventory.GetAsync(fixture.Manager, ingredient.Id);
        Assert.Equal(2d, stock.OnHand.Value, precision: 9);
        Assert.Equal(0d, stock.Reserved.Value);
        Assert.Equal(
            Availability.Limited,
            Assert.Single((await fixture.Menus.GetAsync(fixture.Manager, menu.Id)).Items).Availability);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        private Fixture(string root, ServiceProvider services)
        {
            this.root = root;
            this.services = services;
            Manager = services.GetRequiredService<MixologySessionFactory>().Create(Actor.Manager);
            Ingredients = services.GetRequiredService<IngredientsModule>();
            Inventory = services.GetRequiredService<InventoryModule>();
            Drinks = services.GetRequiredService<DrinksModule>();
            Menus = services.GetRequiredService<MenusModule>();
            Orders = services.GetRequiredService<OrdersModule>();
        }

        public MixologySession Manager { get; }
        public IngredientsModule Ingredients { get; }
        public InventoryModule Inventory { get; }
        public DrinksModule Drinks { get; }
        public MenusModule Menus { get; }
        public OrdersModule Orders { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mixology-order-route-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
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
            await services.GetRequiredService<MixologyStore>().InitializeAsync();
            return new Fixture(root, services);
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
