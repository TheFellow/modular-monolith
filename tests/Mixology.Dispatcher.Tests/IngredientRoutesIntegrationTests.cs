using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Mixology.Application;
using Mixology.Application.Auditing;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Events;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
using Mixology.Modules.Tagging;
using Mixology.Persistence;

namespace Mixology.Dispatcher.Tests;

public sealed class IngredientRoutesIntegrationTests
{
    [Fact]
    public async Task GeneratedRoutesCreateFreshScopesTouchOnlyCurrentDependentsAndDoNotCascade()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient first = await fixture.IngredientAsync("First", Unit.Ounce);
        Ingredient second = await fixture.IngredientAsync("Second", Unit.Ounce);
        Drink firstDrink = await fixture.DrinkAsync("First drink", first.Id, Unit.Ounce);
        Drink secondDrink = await fixture.DrinkAsync("Second drink", second.Id, Unit.Ounce);

        DispatchResult firstResult = await fixture.DispatchAsync(Deleted(first));
        DispatchResult secondResult = await fixture.DispatchAsync(Deleted(second));

        Assert.Equal([firstDrink.EntityUid], firstResult.Touches);
        Assert.Equal([secondDrink.EntityUid], secondResult.Touches);
        Assert.Equal(1, firstResult.EventCount);
        Assert.Equal(1, secondResult.EventCount);
        Assert.Equal(2, fixture.ScopeCreations);
        Assert.Equal(DrinkStatus.ReviewRequired, (await fixture.GetAsync(firstDrink.Id)).Status);
        Assert.Equal(DrinkStatus.ReviewRequired, (await fixture.GetAsync(secondDrink.Id)).Status);
    }

    [Fact]
    public async Task GeneratedUpdatedRouteTouchesPrimaryAndSubstituteDependentsWithoutMutation()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient target = await fixture.IngredientAsync("Target", Unit.Ounce);
        Ingredient other = await fixture.IngredientAsync("Other", Unit.Ounce);
        Drink primary = await fixture.DrinkAsync("Primary", target.Id, Unit.Ounce);
        Drink substitute = await fixture.DrinkAsync(
            "Substitute",
            other.Id,
            Unit.Ounce,
            substitutes: [target.Id]);

        DispatchResult result = await fixture.DispatchAsync(new IngredientUpdated(target with { Name = "Renamed" }));

        Assert.Equal([primary.EntityUid, substitute.EntityUid], result.Touches);
        Assert.Equal(1, result.EventCount);
        Assert.Equal(target.Id, Assert.Single((await fixture.GetAsync(primary.Id)).Recipe.Ingredients).IngredientId);
        Assert.Equal(target.Id, Assert.Single(
            Assert.Single((await fixture.GetAsync(substitute.Id)).Recipe.Ingredients).Substitutes));
    }

    [Fact]
    public async Task HandlerFailureRollsBackBusinessWriteAndCannotCascadeOrTouch()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient retired = await fixture.IngredientAsync("Retired", Unit.Ounce);
        Ingredient replacement = await fixture.IngredientAsync("Replacement", Unit.Milliliter);
        Drink compatible = await fixture.DrinkAsync("A compatible", retired.Id, Unit.Ounce);
        Drink incompatible = await fixture.DrinkAsync("B incompatible", retired.Id, Unit.Piece);
        OperationContext operation = new(Actor.Owner);

        InternalError error = await Assert.ThrowsAsync<InternalError>(() => fixture.DispatchAsync(
            Deleted(retired, replacement, 1),
            operation,
            async context => await context.Session!.Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE drinks SET description = {'X'} WHERE id = {compatible.Id.Value}")));

        Assert.True(AppError.IsInternal(error));
        Assert.True(AppError.IsInvalid(error));
        Assert.Empty(operation.TouchedEntities);
        Assert.Single(operation.Events);
        Assert.Empty((await fixture.GetAsync(compatible.Id)).Description);
        Assert.Equal(retired.Id, Assert.Single((await fixture.GetAsync(compatible.Id)).Recipe.Ingredients).IngredientId);
        Assert.Equal(retired.Id, Assert.Single((await fixture.GetAsync(incompatible.Id)).Recipe.Ingredients).IngredientId);
    }

    private static IngredientDeleted Deleted(
        Ingredient retired,
        Ingredient? replacement = null,
        double ratio = 0)
    {
        DateTimeOffset deletedAt = new(2026, 8, 9, 23, 0, 0, TimeSpan.Zero);
        return new IngredientDeleted(retired with { DeletedAt = deletedAt }, deletedAt, replacement, ratio);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;
        private readonly CountingScopeFactory scopeFactory;
        private readonly DomainEventDispatcher dispatcher;

        private Fixture(string root, ServiceProvider services)
        {
            this.root = root;
            this.services = services;
            Store = services.GetRequiredService<MixologyStore>();
            Drinks = services.GetRequiredService<DrinksModule>();
            Ingredients = services.GetRequiredService<IngredientsModule>();
            scopeFactory = new CountingScopeFactory(services.GetRequiredService<IServiceScopeFactory>());
            dispatcher = new DomainEventDispatcher(scopeFactory, NullLogger<DomainEventDispatcher>.Instance);
        }

        public MixologyStore Store { get; }
        public DrinksModule Drinks { get; }
        public IngredientsModule Ingredients { get; }
        public int ScopeCreations => scopeFactory.Creations;

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mixology-dispatcher-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddMixologyPersistence(Path.Combine(root, "mixology.db"), typeof(Fixture).Assembly);
            collection.AddMixologyApplication();
            collection.AddAuditModule();
            collection.AddIngredientsModule();
            collection.AddDrinksModule();
            collection.AddInventoryModule();
            collection.AddMenusModule();
            collection.AddOrdersModule();
            collection.AddTaggingModule();
            collection.Replace(ServiceDescriptor.Singleton<IActivityRecorder, NoopActivityRecorder>());
            ServiceProvider services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            Fixture fixture = new(root, services);
            await using StoreSession session = await fixture.Store.OpenSessionAsync();
            await session.Context.Database.EnsureCreatedAsync();
            return fixture;
        }

        public Task<Ingredient> IngredientAsync(string name, Unit unit) => Ingredients.CreateAsync(
            Session(),
            new CreateIngredientRequest(name, IngredientCategory.Other, unit));

        public Task<Drink> DrinkAsync(
            string name,
            IngredientId ingredient,
            Unit unit,
            IReadOnlyList<IngredientId>? substitutes = null) => Drinks.CreateAsync(
                Session(),
                new CreateDrinkRequest(
                    name,
                    DrinkCategory.Cocktail,
                    GlassType.Coupe,
                    new Recipe(
                        [new RecipeIngredient(ingredient, Amount.Create(1, unit), substitutes: substitutes)],
                        ["Mix"])));

        public Task<Drink> GetAsync(DrinkId id) => Drinks.GetAsync(Session(), id);

        public Task<DispatchResult> DispatchAsync(object domainEvent) =>
            DispatchAsync(domainEvent, new OperationContext(Actor.Owner), _ => Task.CompletedTask);

        public async Task<DispatchResult> DispatchAsync(
            object domainEvent,
            OperationContext operation,
            Func<OperationContext, Task> body)
        {
            UnitOfWorkMiddleware unitOfWork = new(Store);
            DispatchEventsMiddleware dispatchEvents = new(dispatcher);
            Operation command = Operation.Command("test.ingredient-event");
            await unitOfWork.InvokeAsync(
                operation,
                command,
                context => dispatchEvents.InvokeAsync(context, command, async handlerContext =>
                {
                    await body(handlerContext);
                    handlerContext.AddEvent(domainEvent);
                }));
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

        private MixologySession Session() =>
            services.GetRequiredService<MixologySessionFactory>().Create(Actor.Manager);
    }

    private sealed class CountingScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        public int Creations { get; private set; }

        public IServiceScope CreateScope()
        {
            Creations++;
            return inner.CreateScope();
        }
    }

    private sealed class NoopActivityRecorder : IActivityRecorder
    {
        public Task RecordAsync(OperationContext context, OperationActivity activity)
        {
            _ = context;
            _ = activity;
            return Task.CompletedTask;
        }
    }

    private sealed record DispatchResult(IReadOnlyList<EntityUid> Touches, int EventCount);
}
