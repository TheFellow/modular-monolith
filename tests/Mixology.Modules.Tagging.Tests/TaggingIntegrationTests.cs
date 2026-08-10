using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Tags;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Tagging.Tests;

public sealed class TaggingIntegrationTests
{
    [Fact]
    public async Task OperationalDomainsOwnExactRegistrationsAndAuditRemainsUnsupported()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        string[] supported =
        [
            EntityIds.DrinkType,
            EntityIds.IngredientType,
            EntityIds.InventoryType,
            EntityIds.MenuType,
            EntityIds.OrderType,
        ];

        Assert.All(supported, type => Assert.Equal(type, fixture.Registry.Resolve(type).EntityType));
        Assert.Throws<InvalidError>(() => fixture.Registry.Resolve(EntityIds.AuditEntryType));
    }

    [Fact]
    public async Task OperationalReadsFiltersAndAuthorizationSeeAtomicPersistedTags()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        Ingredient ingredient = await fixture.Ingredients.CreateAsync(
            manager,
            new CreateIngredientRequest("Lime", IngredientCategory.Juice, Unit.Ounce));
        Recipe recipe = new(
            [new RecipeIngredient(ingredient.Id, Amount.Create(1, Unit.Ounce))],
            ["Shake"]);
        Drink drink = await fixture.Drinks.CreateAsync(
            manager,
            new CreateDrinkRequest(
                "Tagged Gimlet",
                DrinkCategory.Cocktail,
                GlassType.Coupe,
                recipe));
        MixologySession sommelier = fixture.Session(Actor.Sommelier);
        await Assert.ThrowsAsync<PermissionError>(() => fixture.Drinks.GetAsync(sommelier, drink.Id));

        await fixture.Tagging.UpsertAsync(
            manager,
            drink.EntityUid,
            new Tag("audience", "sommelier"));
        Drink loaded = await fixture.Drinks.GetAsync(sommelier, drink.Id);
        IReadOnlyList<Drink> filtered = (await fixture.Drinks.ListAsync(
            sommelier,
            new ListDrinksRequest(Filter: "tags.contains(\"audience=sommelier\")"))).Items;
        Drink updated = await fixture.Drinks.UpdateAsync(
            manager,
            new UpdateDrinkRequest(
                drink.Id,
                "Tagged Gimlet Updated",
                drink.Category,
                drink.Glass,
                drink.Recipe,
                drink.Description));

        Assert.Equal(["audience=sommelier"], loaded.Tags.Strings());
        Assert.Equal(drink.Id, Assert.Single(filtered).Id);
        Assert.Equal(["audience=sommelier"], updated.Tags.Strings());
        Assert.Equal(
            ["audience=sommelier"],
            (await fixture.Drinks.GetAsync(sommelier, drink.Id)).Tags.Strings());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        private Fixture(string root, ServiceProvider services)
        {
            this.root = root;
            this.services = services;
            Ingredients = services.GetRequiredService<IngredientsModule>();
            Drinks = services.GetRequiredService<DrinksModule>();
            Tagging = services.GetRequiredService<TaggingModule>();
            Registry = services.GetRequiredService<TagTargetRegistry>();
        }

        public IngredientsModule Ingredients { get; }
        public DrinksModule Drinks { get; }
        public TaggingModule Tagging { get; }
        public TagTargetRegistry Registry { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-tagging-integration", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ServiceCollection services = new();
            services.AddLogging();
            services.AddMixologyPersistence(
                Path.Combine(root, "mixology.db"),
                typeof(MigrationAssemblyMarker).Assembly);
            services.AddMixologyApplication();
            services.AddAuditModule();
            services.AddIngredientsModule();
            services.AddDrinksModule();
            services.AddInventoryModule();
            services.AddMenusModule();
            services.AddOrdersModule();
            services.AddTaggingModule();
            ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            await provider.GetRequiredService<MixologyStore>().InitializeAsync();
            return new Fixture(root, provider);
        }

        public MixologySession Session(Actor actor) =>
            services.GetRequiredService<MixologySessionFactory>().Create(actor);

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
