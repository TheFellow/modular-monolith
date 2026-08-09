using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
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
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Menus.Tests;

public sealed class MenuOperationsIntegrationTests
{
    [Fact]
    public async Task RealOperationsCalculateAvailabilityReadinessCostAndMargin()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.CreateIngredientAsync("Vodka", IngredientCategory.Spirit);
        await fixture.Inventory.SetAsync(
            fixture.Manager,
            new SetInventoryRequest(
                ingredient.Id,
                Amount.Create(10d, Unit.Milliliter),
                new Price(0.05m, Currency.Usd)));
        Drink drink = await fixture.CreateDrinkAsync(
            "Vodka Pour",
            new RecipeIngredient(ingredient.Id, Amount.Create(2d, Unit.Milliliter)));
        Menu menu = await fixture.CreateMenuWithDrinkAsync(drink.Id);
        await fixture.SetMenuPriceAsync(menu.Id, drink.Id, new Price(1m, Currency.Usd));

        ReadinessReport ready = await fixture.Menus.ReadinessAsync(fixture.Manager, menu.Id);
        MenuAnalysis analysis = await fixture.Menus.AnalyzeAsync(fixture.Manager, menu.Id, 0.75d);

        Assert.Empty(ready.Findings);
        MenuItemAnalysis item = Assert.Single(analysis.Items);
        Assert.Equal("Vodka Pour", item.Name);
        Assert.Equal(Availability.Available, item.Availability);
        Assert.Equal(new Price(0.10m, Currency.Usd), item.Cost);
        Assert.Equal(new Price(0.40m, Currency.Usd), item.SuggestedPrice);
        Assert.Equal(0.9d, item.Margin!.Value, 6);
        Assert.Equal(0.9d, analysis.AverageMargin!.Value, 6);
        Assert.Equal(1, analysis.AvailableCount);

        await fixture.Inventory.SetAsync(
            fixture.Manager,
            new SetInventoryRequest(
                ingredient.Id,
                Amount.Create(5d, Unit.Milliliter),
                new Price(0.05m, Currency.Usd)));
        ReadinessReport limited = await fixture.Menus.ReadinessAsync(fixture.Manager, menu.Id);
        ReadinessFinding finding = Assert.Single(limited.Findings);
        Assert.Equal(ReadinessSeverity.Warning, finding.Severity);
        Assert.Equal(ReadinessCode.LowStock, finding.Code);
    }

    [Fact]
    public async Task RealOperationsChooseDeterministicTemporarySubstitutions()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient lime = await fixture.CreateIngredientAsync("Lime Juice", IngredientCategory.Juice);
        Ingredient lemon = await fixture.CreateIngredientAsync("Lemon Juice", IngredientCategory.Juice);
        await fixture.Inventory.SetAsync(
            fixture.Manager,
            new SetInventoryRequest(
                lemon.Id,
                Amount.Create(10d, Unit.Milliliter),
                new Price(0.02m, Currency.Usd)));
        Drink drink = await fixture.CreateDrinkAsync(
            "Citrus",
            new RecipeIngredient(
                lime.Id,
                Amount.Create(1d, Unit.Milliliter),
                substitutes: [lemon.Id]));
        Menu menu = await fixture.CreateMenuWithDrinkAsync(drink.Id);

        ReadinessReport readiness = await fixture.Menus.ReadinessAsync(fixture.Manager, menu.Id);
        MenuAnalysis analysis = await fixture.Menus.AnalyzeAsync(fixture.Manager, menu.Id, 0.9d);

        ReadinessFinding finding = Assert.Single(readiness.Findings);
        Assert.Equal(ReadinessSeverity.Blocker, finding.Severity);
        Assert.Equal(ReadinessCode.TemporarySubstitution, finding.Code);
        Assert.Equal(lime.Id, finding.IngredientId);
        MenuItemAnalysis item = Assert.Single(analysis.Items);
        AppliedSubstitution substitution = Assert.Single(item.Substitutions);
        Assert.Equal(lime.Id, substitution.OriginalIngredientId);
        Assert.Equal(lemon.Id, substitution.SubstituteIngredientId);
        Assert.Equal(Availability.Limited, item.Availability);
        Assert.Equal(new Price(0.02m, Currency.Usd), item.Cost);
        Assert.Equal(new Price(0.20m, Currency.Usd), item.SuggestedPrice);
    }

    [Fact]
    public async Task ReadinessIsStrictWhileAnalyticsDegradesDependencyFailure()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.CreateIngredientAsync("Gin", IngredientCategory.Spirit);
        await fixture.Inventory.SetAsync(
            fixture.Manager,
            new SetInventoryRequest(
                ingredient.Id,
                Amount.Create(10d, Unit.Milliliter),
                new Price(0.04m, Currency.Usd)));
        Drink drink = await fixture.CreateDrinkAsync(
            "Gin Pour",
            new RecipeIngredient(ingredient.Id, Amount.Create(1d, Unit.Milliliter)));
        Menu menu = await fixture.CreateMenuWithDrinkAsync(drink.Id);
        await fixture.DropInventoryAsync();

        await Assert.ThrowsAsync<InternalError>(() => fixture.Menus.ReadinessAsync(fixture.Manager, menu.Id));
        MenuAnalysis analysis = await fixture.Menus.AnalyzeAsync(fixture.Manager, menu.Id, 0.7d);
        MenuItemAnalysis item = Assert.Single(analysis.Items);

        Assert.Equal(Availability.Unavailable, item.Availability);
        Assert.True(item.CostUnknown);
        Assert.Null(item.Cost);
        Assert.Null(item.SuggestedPrice);
        Assert.Equal(0, analysis.AvailableCount);
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
        }

        public MixologySession Manager { get; }
        public IngredientsModule Ingredients { get; }
        public InventoryModule Inventory { get; }
        public DrinksModule Drinks { get; }
        public MenusModule Menus { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-menu-operations-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ServiceCollection collection = new();
            collection.AddLogging();
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
            return new Fixture(root, services);
        }

        public Task<Ingredient> CreateIngredientAsync(string name, IngredientCategory category) =>
            Ingredients.CreateAsync(Manager, new CreateIngredientRequest(name, category, Unit.Milliliter));

        public Task<Drink> CreateDrinkAsync(string name, params RecipeIngredient[] ingredients) =>
            Drinks.CreateAsync(
                Manager,
                new CreateDrinkRequest(
                    name,
                    DrinkCategory.Cocktail,
                    GlassType.Coupe,
                    new Recipe(ingredients, ["Stir"])));

        public async Task<Menu> CreateMenuWithDrinkAsync(DrinkId drinkId)
        {
            Menu menu = await Menus.CreateAsync(Manager, new CreateMenuRequest($"Menu {drinkId}"));
            return await Menus.AddDrinkAsync(Manager, new AddMenuItemRequest(menu.Id, drinkId));
        }

        public async Task SetMenuPriceAsync(MenuId menuId, DrinkId drinkId, Price price)
        {
            await using StoreSession session = await services.GetRequiredService<MixologyStore>().OpenSessionAsync();
            await session.Context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE menu_items
                SET price_amount = {price.Amount}, price_currency = {price.Currency.Code}
                WHERE menu_id = {menuId.Value} AND drink_id = {drinkId.Value}
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
}
