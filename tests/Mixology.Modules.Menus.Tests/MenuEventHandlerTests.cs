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
using Mixology.Modules.Drinks.Events;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Queries;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Events;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Events;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Menus.Events;
using Mixology.Modules.Menus.Handlers;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Ports;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Tagging;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Menus.Tests;

public sealed class MenuEventHandlerTests
{
    [Fact]
    public async Task DrinkDeletedRemovesOnlyTheDeletedDrinkAndPreservesPublishedState()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient targetIngredient = await fixture.IngredientAsync("Target ingredient");
        Ingredient otherIngredient = await fixture.IngredientAsync("Other ingredient");
        Drink target = await fixture.DrinkAsync("Target drink", targetIngredient.Id);
        Drink survivor = await fixture.DrinkAsync("Survivor drink", otherIngredient.Id);
        Menu affected = await fixture.MenuAsync("Affected", target.Id, survivor.Id);
        Menu unrelated = await fixture.MenuAsync("Unrelated", survivor.Id);
        await fixture.SetStateAsync(affected.Id, MenuStatus.Published, Availability.Available);
        await fixture.SetStateAsync(unrelated.Id, MenuStatus.Published, Availability.Available);

        DispatchResult result = await fixture.DispatchAsync(
            new DrinkDeleted(target, fixture.Now));

        Menu rewritten = await fixture.GetAsync(affected.Id);
        Assert.Equal(MenuStatus.Published, rewritten.Status);
        Assert.Equal(survivor.Id, Assert.Single(rewritten.Items).DrinkId);
        Assert.Equal(survivor.Id, Assert.Single((await fixture.GetAsync(unrelated.Id)).Items).DrinkId);
        Assert.Equal([affected.EntityUid], result.Touches);
        Assert.Equal(1, result.EventCount);
    }

    [Fact]
    public async Task DrinkUpdatedRecalculatesOnlyAffectedPublishedMenus()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient targetIngredient = await fixture.IngredientAsync("Target ingredient");
        Ingredient otherIngredient = await fixture.IngredientAsync("Other ingredient");
        Drink target = await fixture.DrinkAsync("Target drink", targetIngredient.Id);
        Drink other = await fixture.DrinkAsync("Other drink", otherIngredient.Id);
        Menu published = await fixture.MenuAsync("Published", target.Id, other.Id);
        Menu draft = await fixture.MenuAsync("Draft", target.Id);
        Menu unrelated = await fixture.MenuAsync("Unrelated", other.Id);
        await fixture.SetStateAsync(published.Id, MenuStatus.Published, Availability.Available);
        await fixture.SetStateAsync(draft.Id, MenuStatus.Draft, Availability.Available);
        await fixture.SetStateAsync(unrelated.Id, MenuStatus.Published, Availability.Available);
        fixture.Availability.Set(target.Id, Availability.Unavailable);

        DispatchResult result = await fixture.DispatchAsync(new DrinkUpdated(target));

        Assert.Equal(Availability.Unavailable, Item(await fixture.GetAsync(published.Id), target.Id).Availability);
        Assert.Equal(Availability.Available, Item(await fixture.GetAsync(published.Id), other.Id).Availability);
        Assert.Equal(Availability.Available, Item(await fixture.GetAsync(draft.Id), target.Id).Availability);
        Assert.Equal(Availability.Available, Item(await fixture.GetAsync(unrelated.Id), other.Id).Availability);
        Assert.Equal([published.EntityUid], result.Touches);
        Assert.Equal(1, result.EventCount);
    }

    [Fact]
    public async Task IngredientRetirementPreparesRelationshipsThenUsesPostPrepareAvailability()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient retired = await fixture.IngredientAsync("Retired ingredient");
        Drink drink = await fixture.DrinkAsync("Affected drink", retired.Id);
        Menu menu = await fixture.MenuAsync("Published", drink.Id);
        await fixture.SetStateAsync(menu.Id, MenuStatus.Published, Availability.Available);
        fixture.Availability.Set(drink.Id, Availability.Available);
        bool preparedBeforeMutation = false;

        DispatchResult result = await fixture.DispatchAsync(
            new IngredientDeleted(retired, fixture.Now, null, 0d),
            async _ =>
            {
                preparedBeforeMutation = Item(await fixture.GetAsync(menu.Id), drink.Id).Availability
                    == Availability.Available;
                fixture.Availability.Set(drink.Id, Availability.Unavailable);
            });

        Menu degraded = await fixture.GetAsync(menu.Id);
        Assert.True(preparedBeforeMutation);
        Assert.Equal(MenuStatus.Published, degraded.Status);
        Assert.Equal(Availability.Unavailable, Item(degraded, drink.Id).Availability);
        Assert.Equal([menu.EntityUid], result.Touches);
        Assert.Equal(1, result.EventCount);
    }

    [Fact]
    public async Task IngredientUpdatedTouchesEveryDependentMenuOnceWithoutMutation()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.IngredientAsync("Ingredient");
        Drink first = await fixture.DrinkAsync("First drink", ingredient.Id);
        Drink second = await fixture.DrinkAsync("Second drink", ingredient.Id);
        Menu firstMenu = await fixture.MenuAsync("First menu", first.Id, second.Id);
        Menu secondMenu = await fixture.MenuAsync("Second menu", first.Id);
        await fixture.SetStateAsync(firstMenu.Id, MenuStatus.Published, Availability.Available);
        await fixture.SetStateAsync(secondMenu.Id, MenuStatus.Draft, Availability.Limited);

        DispatchResult result = await fixture.DispatchAsync(new IngredientUpdated(ingredient));

        Assert.Equal(
            new HashSet<EntityUid>([firstMenu.EntityUid, secondMenu.EntityUid]),
            result.Touches.ToHashSet());
        Assert.All((await fixture.GetAsync(firstMenu.Id)).Items, item =>
            Assert.Equal(Availability.Available, item.Availability));
        Assert.Equal(Availability.Limited, Assert.Single((await fixture.GetAsync(secondMenu.Id)).Items).Availability);
        Assert.Equal(1, result.EventCount);
    }

    [Fact]
    public async Task StockAdjustedRecalculatesOnlyPublishedMenusUsingTheIngredient()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient targetIngredient = await fixture.IngredientAsync("Target ingredient");
        Ingredient otherIngredient = await fixture.IngredientAsync("Other ingredient");
        Drink target = await fixture.DrinkAsync("Target drink", targetIngredient.Id);
        Drink other = await fixture.DrinkAsync("Other drink", otherIngredient.Id);
        Menu published = await fixture.MenuAsync("Published", target.Id, other.Id);
        Menu draft = await fixture.MenuAsync("Draft", target.Id);
        Menu unrelated = await fixture.MenuAsync("Unrelated", other.Id);
        await fixture.SetStateAsync(published.Id, MenuStatus.Published, Availability.Available);
        await fixture.SetStateAsync(draft.Id, MenuStatus.Draft, Availability.Available);
        await fixture.SetStateAsync(unrelated.Id, MenuStatus.Published, Availability.Available);
        fixture.Availability.Set(target.Id, Availability.Unavailable);
        InventoryStock stock = new(
            InventoryId.New(),
            targetIngredient.Id,
            Amount.Create(0d, Unit.Ounce),
            Amount.Create(0d, Unit.Ounce),
            null,
            fixture.Now,
            TagCollection.Empty);

        DispatchResult result = await fixture.DispatchAsync(new StockAdjusted(stock, "used", true));

        Menu recalculated = await fixture.GetAsync(published.Id);
        Assert.Equal(MenuStatus.Published, recalculated.Status);
        Assert.Equal(Availability.Unavailable, Item(recalculated, target.Id).Availability);
        Assert.Equal(Availability.Available, Item(recalculated, other.Id).Availability);
        Assert.Equal(Availability.Available, Item(await fixture.GetAsync(draft.Id), target.Id).Availability);
        Assert.Equal(Availability.Available, Item(await fixture.GetAsync(unrelated.Id), other.Id).Availability);
        Assert.Equal([published.EntityUid], result.Touches);
        Assert.Equal(1, result.EventCount);
    }

    [Fact]
    public async Task MenuPublishedRecomputesItselfWithoutChangingPublishedState()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.IngredientAsync("Ingredient");
        Drink drink = await fixture.DrinkAsync("Drink", ingredient.Id);
        Menu menu = await fixture.MenuAsync("Menu", drink.Id);
        await fixture.SetStateAsync(menu.Id, MenuStatus.Published, Availability.Available);
        Menu published = await fixture.GetAsync(menu.Id);
        fixture.Availability.Set(drink.Id, Availability.Unavailable);

        DispatchResult result = await fixture.DispatchAsync(new MenuPublished(published));

        Menu recalculated = await fixture.GetAsync(menu.Id);
        Assert.Equal(MenuStatus.Published, recalculated.Status);
        Assert.Equal(Availability.Unavailable, Assert.Single(recalculated.Items).Availability);
        Assert.Equal([menu.EntityUid], result.Touches);
        Assert.Equal(1, result.EventCount);
    }

    [Fact]
    public async Task PersistenceFailuresRetainTypedHandlerContext()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.IngredientAsync("Ingredient");
        Drink drink = await fixture.DrinkAsync("Drink", ingredient.Id);
        await fixture.DropMenuItemsAsync();

        Exception error = await fixture.DispatchFailureAsync(new DrinkUpdated(drink));

        Assert.True(AppError.IsInternal(error));
        Assert.Contains(
            Exceptions(error).OfType<InternalError>(),
            candidate => candidate.Message == "load menus by drink");
        Assert.Contains(Exceptions(error), static candidate => candidate is SqliteException);
    }

    [Fact]
    public async Task CancellationRemainsClassifiableThroughDispatchWrapping()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.IngredientAsync("Ingredient");
        Drink drink = await fixture.DrinkAsync("Drink", ingredient.Id);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Exception error = await fixture.DispatchFailureAsync(
            new DrinkUpdated(drink),
            cancellation.Token);

        Assert.True(AppError.IsCancellation(error));
        Assert.Contains(Exceptions(error), static candidate => candidate is OperationCanceledException);
    }

    private static MenuItem Item(Menu menu, DrinkId id) =>
        Assert.Single(menu.Items, item => item.DrinkId == id);

    private static IEnumerable<Exception> Exceptions(Exception exception)
    {
        yield return exception;
        if (exception.InnerException is { } inner)
        {
            foreach (Exception nested in Exceptions(inner))
            {
                yield return nested;
            }
        }
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
            Session = services.GetRequiredService<MixologySessionFactory>().Create(Actor.Manager);
            Ingredients = services.GetRequiredService<IngredientsModule>();
            Drinks = services.GetRequiredService<DrinksModule>();
            Menus = services.GetRequiredService<MenusModule>();
            DrinkQueries = services.GetRequiredService<DrinkQueries>();
        }

        public DateTimeOffset Now { get; } = new(2026, 8, 9, 22, 0, 0, TimeSpan.Zero);
        public FakeMenuOperations Availability { get; } = new();
        public MixologyStore Store { get; }
        public MixologySession Session { get; }
        public IngredientsModule Ingredients { get; }
        public DrinksModule Drinks { get; }
        public MenusModule Menus { get; }
        public DrinkQueries DrinkQueries { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mixology-menu-event-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddMixologyPersistence(
                Path.Combine(root, "mixology.db"),
                typeof(MigrationAssemblyMarker).Assembly);
            collection.AddMixologyApplication();
            collection.AddTaggingModule();
            collection.AddAuditModule();
            collection.AddIngredientsModule();
            collection.AddInventoryModule();
            collection.AddDrinksModule();
            collection.AddMenusModule();
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

        public Task<Ingredient> IngredientAsync(string name) => Ingredients.CreateAsync(
            Session,
            new CreateIngredientRequest(name, IngredientCategory.Other, Unit.Ounce));

        public Task<Drink> DrinkAsync(string name, IngredientId ingredientId) => Drinks.CreateAsync(
            Session,
            new CreateDrinkRequest(
                name,
                DrinkCategory.Cocktail,
                GlassType.Coupe,
                new Recipe(
                    [new RecipeIngredient(ingredientId, Amount.Create(1d, Unit.Ounce))],
                    ["Mix"])));

        public async Task<Menu> MenuAsync(string name, params DrinkId[] drinkIds)
        {
            Menu menu = await Menus.CreateAsync(Session, new CreateMenuRequest(name));
            foreach (DrinkId drinkId in drinkIds)
            {
                menu = await Menus.AddDrinkAsync(Session, new AddMenuItemRequest(menu.Id, drinkId));
            }

            return menu;
        }

        public Task<Menu> GetAsync(MenuId id) => Menus.GetAsync(Session, id);

        public async Task SetStateAsync(MenuId id, MenuStatus status, Availability availability)
        {
            await using StoreSession storeSession = await Store.OpenSessionAsync();
            await storeSession.Context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE menus
                SET status = {status.Value},
                    published_at_utc = CASE WHEN {status.Value} = 'published' THEN {Now.UtcDateTime} ELSE NULL END
                WHERE id = {id.Value};
                UPDATE menu_items
                SET availability = {availability.Value}
                WHERE menu_id = {id.Value};
                """);
        }

        public async Task DropMenuItemsAsync()
        {
            await using StoreSession storeSession = await Store.OpenSessionAsync();
            await storeSession.Context.Database.ExecuteSqlRawAsync("DROP TABLE menu_items");
        }

        public async Task<DispatchResult> DispatchAsync(
            object domainEvent,
            Func<EventHandlerContext, Task>? afterPrepare = null)
        {
            HandlerDispatcher dispatcher = new(DrinkQueries, Availability, afterPrepare);
            await using StoreSession storeSession = await Store.OpenSessionAsync(CancellationToken.None);
            await storeSession.BeginWriteAsync(CancellationToken.None);
            OperationContext operation = new(Actor.Owner, storeSession);
            try
            {
                await new DispatchEventsMiddleware(dispatcher).InvokeAsync(
                    operation,
                    Operation.Command("test.menu-event"),
                    context =>
                    {
                        context.AddEvent(domainEvent);
                        return Task.CompletedTask;
                    });
                await storeSession.Context.SaveChangesAsync();
                await storeSession.CommitAsync();
            }
            catch
            {
                await storeSession.RollbackAsync(CancellationToken.None);
                throw;
            }

            return new DispatchResult(operation.TouchedEntities.ToArray(), operation.Events.Count);
        }

        public async Task<Exception> DispatchFailureAsync(
            object domainEvent,
            CancellationToken cancellationToken = default)
        {
            HandlerDispatcher dispatcher = new(DrinkQueries, Availability, afterPrepare: null);
            await using StoreSession storeSession = await Store.OpenSessionAsync(CancellationToken.None);
            await storeSession.BeginWriteAsync(CancellationToken.None);
            OperationContext operation = new(Actor.Owner, storeSession, cancellationToken);
            try
            {
                await new DispatchEventsMiddleware(dispatcher).InvokeAsync(
                    operation,
                    Operation.Command("test.menu-event.failure"),
                    context =>
                    {
                        context.AddEvent(domainEvent);
                        return Task.CompletedTask;
                    });
            }
            catch (Exception exception)
            {
                await storeSession.RollbackAsync(CancellationToken.None);
                return exception;
            }

            await storeSession.RollbackAsync(CancellationToken.None);
            throw new InvalidOperationException("Expected event dispatch to fail.");
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

    private sealed class HandlerDispatcher(
        DrinkQueries drinks,
        IMenuOperations operations,
        Func<EventHandlerContext, Task>? afterPrepare) : IDomainEventDispatcher
    {
        public async Task DispatchAsync(EventHandlerContext context, object domainEvent)
        {
            switch (domainEvent)
            {
                case DrinkDeleted deleted:
                    await new Mixology.Modules.Menus.Handlers.DrinkDeletedHandler()
                        .HandleAsync(context, deleted);
                    break;
                case DrinkUpdated updated:
                    await new Mixology.Modules.Menus.Handlers.DrinkUpdatedHandler(operations)
                        .HandleAsync(context, updated);
                    break;
                case IngredientDeleted deleted:
                    Mixology.Modules.Menus.Handlers.IngredientDeletedHandler handler =
                        new(drinks, operations);
                    await handler.PrepareAsync(context, deleted);
                    if (afterPrepare is not null)
                    {
                        await afterPrepare(context);
                    }

                    await handler.HandleAsync(context, deleted);
                    break;
                case IngredientUpdated updated:
                    await new Mixology.Modules.Menus.Handlers.IngredientUpdatedHandler(drinks)
                        .HandleAsync(context, updated);
                    break;
                case StockAdjusted adjusted:
                    await new StockAdjustedHandler(drinks, operations).HandleAsync(context, adjusted);
                    break;
                case MenuPublished published:
                    await new MenuPublishedHandler(operations).HandleAsync(context, published);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected event {domainEvent.GetType().Name}.");
            }
        }
    }

    private sealed class FakeMenuOperations : IMenuOperations
    {
        private readonly Dictionary<DrinkId, Availability> availability = [];

        public void Set(DrinkId id, Availability value) => availability[id] = value;

        public ValueTask<Availability> GetAvailabilityAsync(
            StoreSession session,
            DrinkId id,
            CancellationToken cancellationToken = default)
        {
            _ = session;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(availability.GetValueOrDefault(id, Availability.Available));
        }

        public ValueTask<MenuDrink> GetDrinkAsync(
            StoreSession session,
            DrinkId id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<ReadinessReport> GetReadinessAsync(
            StoreSession session,
            Menu menu,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<MenuAnalysis> AnalyzeAsync(
            StoreSession session,
            Menu menu,
            double targetMargin,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<IReadOnlyList<IngredientFulfillment>?> FulfillIngredientsAsync(
            StoreSession session,
            IReadOnlyList<RecipeIngredient> requirements,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed record DispatchResult(IReadOnlyList<EntityUid> Touches, int EventCount);
}
