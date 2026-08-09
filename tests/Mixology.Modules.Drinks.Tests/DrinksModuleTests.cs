using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks.Events;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
using Mixology.Modules.Tagging;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Drinks.Tests;

public sealed class DrinksModuleTests
{
    [Fact]
    public async Task CrudPersistsRecipesEmitsEventsAndSoftDeletes()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        Ingredient lime = await fixture.Ingredient("Lime Juice");
        Ingredient lemon = await fixture.Ingredient("Lemon Juice");
        Drink created = await fixture.Drinks.CreateAsync(
            manager,
            Create("  Margarita  ", lime.Id, garnish: "  Lime wheel  "));
        Drink loaded = await fixture.Drinks.GetAsync(fixture.Session(Actor.Anonymous), created.Id);

        Drink updated = await fixture.Drinks.UpdateAsync(
            manager,
            new UpdateDrinkRequest(
                created.Id,
                "Margarita",
                DrinkCategory.Cocktail,
                GlassType.Coupe,
                Recipe(lemon.Id, "Shake hard", "Lemon wheel"),
                "  Bright  "));
        Drink deleted = await fixture.Drinks.DeleteAsync(manager, created.Id);

        Assert.Equal("Margarita", loaded.Name);
        Assert.Equal("Lime wheel", loaded.Recipe.Garnish);
        Assert.Equal(lime.Id, Assert.Single(loaded.Recipe.Ingredients).IngredientId);
        Assert.Equal(lemon.Id, Assert.Single(updated.Recipe.Ingredients).IngredientId);
        Assert.Equal("Bright", updated.Description);
        Assert.Equal(DrinkStatus.Active, updated.Status);
        Assert.Equal(fixture.Now, deleted.DeletedAt);
        await Assert.ThrowsAsync<NotFoundError>(() => fixture.Drinks.GetAsync(manager, created.Id));
        Assert.Collection(
            fixture.Dispatcher.Events.Where(value => value is DrinkCreated or DrinkUpdated or DrinkDeleted),
            value => Assert.IsType<DrinkCreated>(value),
            value => Assert.IsType<DrinkUpdated>(value),
            value => Assert.IsType<DrinkDeleted>(value));
    }

    [Fact]
    public async Task RecipeReferencesDistinguishOptionalIngredientsAndSubstitutes()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        IngredientId absent = IngredientId.New();
        CreateDrinkRequest required = Create("Required missing", absent);
        CreateDrinkRequest optional = required with
        {
            Name = "Optional missing",
            Recipe = new Recipe(
                [new RecipeIngredient(absent, Amount.Create(1, Unit.Ounce), optional: true)],
                ["Serve"]),
        };
        CreateDrinkRequest substitute = optional with
        {
            Name = "Missing substitute",
            Recipe = new Recipe(
                [new RecipeIngredient(absent, Amount.Create(1, Unit.Ounce), true, [IngredientId.New()])],
                ["Serve"]),
        };

        InvalidError requiredError = await Assert.ThrowsAsync<InvalidError>(
            () => fixture.Drinks.CreateAsync(manager, required));
        Drink created = await fixture.Drinks.CreateAsync(manager, optional);
        InvalidError substituteError = await Assert.ThrowsAsync<InvalidError>(
            () => fixture.Drinks.CreateAsync(manager, substitute));

        Assert.True(AppError.IsNotFound(requiredError));
        Assert.True(AppError.IsNotFound(substituteError));
        Assert.Equal("Optional missing", created.Name);
    }

    [Fact]
    public async Task DuplicateNamePreservesTheTypedPersistenceConflict()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient baseIngredient = await fixture.Ingredient("Base");
        MixologySession manager = fixture.Session(Actor.Manager);
        _ = await fixture.Drinks.CreateAsync(manager, Create("Duplicate", baseIngredient.Id));

        ConflictError error = await Assert.ThrowsAsync<ConflictError>(
            () => fixture.Drinks.CreateAsync(manager, Create("Duplicate", baseIngredient.Id)));

        Assert.Equal(ErrorKind.Conflict, error.Kind);
    }

    [Fact]
    public async Task ListAppliesNestedResidualFiltersCursorPagingAndVisibleCount()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        Ingredient baseIngredient = await fixture.Ingredient("Gin");
        await fixture.Drinks.CreateAsync(manager, Create("Gin Fizz", baseIngredient.Id, "Lemon twist"));
        await fixture.Drinks.CreateAsync(manager, Create("Old Fashioned", baseIngredient.Id, "Orange peel"));
        await fixture.Drinks.CreateAsync(manager, Create("Gimlet", baseIngredient.Id, "Lime wheel"));
        ListDrinksRequest filtered = new(
            Filter: "recipe.garnish.startsWith(\"L\") && status == \"active\"",
            Limit: 1);

        Page<Drink> first = await fixture.Drinks.ListAsync(fixture.Session(Actor.Anonymous), filtered);
        Page<Drink> second = await fixture.Drinks.ListAsync(
            fixture.Session(Actor.Anonymous),
            filtered with { Cursor = first.Next });

        Assert.Single(first.Items);
        Assert.False(first.Next.IsEmpty);
        Assert.Single(second.Items);
        Assert.True(second.Next.IsEmpty);
        Assert.Equal(2, await fixture.Drinks.CountAsync(fixture.Session(Actor.Anonymous), filtered));
        Assert.All(first.Items.Concat(second.Items), drink =>
            Assert.StartsWith("L", drink.Recipe.Garnish, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AuthorizationElidesListsAndChecksBothStatesOfAnUpdate()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient baseIngredient = await fixture.Ingredient("Base");
        MixologySession manager = fixture.Session(Actor.Manager);
        Drink wine = await fixture.Drinks.CreateAsync(
            manager,
            Create("House Wine", baseIngredient.Id) with { Category = DrinkCategory.Wine });
        Drink cocktail = await fixture.Drinks.CreateAsync(manager, Create("House Cocktail", baseIngredient.Id));

        Page<Drink> sommelier = await fixture.Drinks.ListAsync(
            fixture.Session(Actor.Sommelier), new ListDrinksRequest());
        Page<Drink> bartender = await fixture.Drinks.ListAsync(
            fixture.Session(Actor.Bartender), new ListDrinksRequest());
        Page<Drink> anonymous = await fixture.Drinks.ListAsync(
            fixture.Session(Actor.Anonymous), new ListDrinksRequest());

        Assert.Equal(wine.Id, Assert.Single(sommelier.Items).Id);
        Assert.Equal(cocktail.Id, Assert.Single(bartender.Items).Id);
        Assert.Equal(2, anonymous.Items.Count);
        await Assert.ThrowsAsync<PermissionError>(() => fixture.Drinks.UpdateAsync(
            fixture.Session(Actor.Sommelier),
            new UpdateDrinkRequest(
                wine.Id,
                wine.Name,
                DrinkCategory.Cocktail,
                wine.Glass,
                wine.Recipe,
                wine.Description)));
        Assert.Equal(DrinkCategory.Wine, (await fixture.Drinks.GetAsync(manager, wine.Id)).Category);
    }

    [Fact]
    public async Task ListByIngredientIncludesSubstitutesAndActiveIdsDeduplicate()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient primary = await fixture.Ingredient("Primary");
        Ingredient substitute = await fixture.Ingredient("Substitute");
        MixologySession manager = fixture.Session(Actor.Manager);
        Drink drink = await fixture.Drinks.CreateAsync(
            manager,
            new CreateDrinkRequest(
                "Substitutable",
                DrinkCategory.Cocktail,
                GlassType.Coupe,
                new Recipe(
                    [new RecipeIngredient(primary.Id, Amount.Create(1, Unit.Ounce), substitutes: [substitute.Id])],
                    ["Serve"]),
                string.Empty));

        Assert.Equal(drink.Id, Assert.Single(await fixture.Drinks.ListByIngredientAsync(manager, substitute.Id)).Id);
        await fixture.Drinks.DeleteAsync(manager, drink.Id);
        IReadOnlySet<DrinkId> active = await fixture.Drinks.ActiveIdsAsync(
            manager, [drink.Id, drink.Id, DrinkId.New()]);
        Assert.Empty(active);
    }

    private static CreateDrinkRequest Create(
        string name,
        IngredientId ingredient,
        string garnish = "") => new(
            name,
            DrinkCategory.Cocktail,
            GlassType.Coupe,
            Recipe(ingredient, "Shake", garnish),
            "Classic");

    private static Recipe Recipe(IngredientId ingredient, string step, string garnish) => new(
        [new RecipeIngredient(ingredient, Amount.Create(1, Unit.Ounce))],
        [step],
        garnish);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        private Fixture(
            string root,
            ServiceProvider services,
            DateTimeOffset now,
            RecordingDispatcher dispatcher)
        {
            this.root = root;
            this.services = services;
            Now = now;
            Dispatcher = dispatcher;
            Drinks = services.GetRequiredService<DrinksModule>();
            Ingredients = services.GetRequiredService<IngredientsModule>();
        }

        public DateTimeOffset Now { get; }
        public RecordingDispatcher Dispatcher { get; }
        public DrinksModule Drinks { get; }
        public IngredientsModule Ingredients { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-drinks-tests", Guid.NewGuid().ToString("N"));
            string database = Path.Combine(root, "mixology.db");
            Directory.CreateDirectory(root);
            DateTimeOffset now = new(2026, 8, 9, 21, 0, 0, TimeSpan.Zero);
            RecordingDispatcher dispatcher = new();
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            collection.AddSingleton<IDomainEventDispatcher>(dispatcher);
            collection.AddMixologyPersistence(database, typeof(MigrationAssemblyMarker).Assembly);
            collection.AddMixologyApplication();
            collection.AddAuditModule();
            collection.AddIngredientsModule();
            collection.AddInventoryModule();
            collection.AddMenusModule();
            collection.AddOrdersModule();
            collection.AddTaggingModule();
            collection.AddDrinksModule();
            ServiceProvider services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            await services.GetRequiredService<MixologyStore>().InitializeAsync();
            return new Fixture(root, services, now, dispatcher);
        }

        public MixologySession Session(Actor actor) =>
            services.GetRequiredService<MixologySessionFactory>().Create(actor);

        public Task<Ingredient> Ingredient(string name) => Ingredients.CreateAsync(
            Session(Actor.Manager),
            new CreateIngredientRequest(name, IngredientCategory.Other, Unit.Ounce));

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

    public sealed class RecordingDispatcher : IDomainEventDispatcher
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
