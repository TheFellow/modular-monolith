using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks.Handlers;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Queries;
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
using Xunit;

namespace Mixology.Modules.Drinks.Tests;

public sealed class IngredientEventHandlerTests
{
    [Fact]
    public async Task IngredientUpdatedTouchesEveryDependentWithoutMutationOrCascadingEvents()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient target = await fixture.IngredientAsync("Target", Unit.Ounce);
        Ingredient other = await fixture.IngredientAsync("Other", Unit.Ounce);
        Drink primary = await fixture.DrinkAsync("Primary", Recipe(target.Id));
        Drink substitute = await fixture.DrinkAsync(
            "Substitute",
            Recipe(other.Id, substitutes: [target.Id]));
        Drink survivor = await fixture.DrinkAsync("Survivor", Recipe(other.Id));

        DispatchResult result = await fixture.DispatchAsync(new IngredientUpdated(target with { Name = "Renamed" }));

        Assert.Equal([primary.EntityUid, substitute.EntityUid], result.Touches);
        Assert.Equal(1, result.EventCount);
        Assert.Equal(target.Id, Assert.Single((await fixture.GetAsync(primary.Id)).Recipe.Ingredients).IngredientId);
        Assert.Equal(target.Id, Assert.Single(
            Assert.Single((await fixture.GetAsync(substitute.Id)).Recipe.Ingredients).Substitutes));
        Assert.Equal(other.Id, Assert.Single((await fixture.GetAsync(survivor.Id)).Recipe.Ingredients).IngredientId);
    }

    [Fact]
    public async Task RetirementWithoutReplacementRemovesOptionalReferencesAndMarksRequiredForReview()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient retired = await fixture.IngredientAsync("Retired", Unit.Ounce);
        Ingredient baseIngredient = await fixture.IngredientAsync("Base", Unit.Ounce);
        Drink drink = await fixture.DrinkAsync(
            "Flexible",
            new Recipe(
            [
                new RecipeIngredient(
                    baseIngredient.Id,
                    Amount.Create(1, Unit.Ounce),
                    substitutes: [retired.Id, retired.Id, baseIngredient.Id]),
                new RecipeIngredient(retired.Id, Amount.Create(0.5, Unit.Ounce), optional: true),
                new RecipeIngredient(retired.Id, Amount.Create(1, Unit.Ounce)),
            ],
            ["Mix"]));
        IngredientDeleted domainEvent = Deleted(retired);
        Drink? prepared = null;

        DispatchResult result = await fixture.DispatchAsync(
            domainEvent,
            async context => prepared = await fixture.Queries.GetAsync(context.Session, drink.Id));

        Drink rewritten = await fixture.GetAsync(drink.Id);
        Assert.Equal(DrinkStatus.Active, prepared?.Status);
        Assert.Equal(3, prepared?.Recipe.Ingredients.Count);
        Assert.Equal(DrinkStatus.ReviewRequired, rewritten.Status);
        Assert.Collection(
            rewritten.Recipe.Ingredients,
            ingredient =>
            {
                Assert.Equal(baseIngredient.Id, ingredient.IngredientId);
                Assert.Empty(ingredient.Substitutes);
            },
            ingredient =>
            {
                Assert.Equal(retired.Id, ingredient.IngredientId);
                Assert.False(ingredient.Optional);
            });
        Assert.Equal([drink.EntityUid], result.Touches);
        Assert.Equal(1, result.EventCount);
    }

    [Fact]
    public async Task ExplicitReplacementConvertsRatioAndCompactsPrimaryAndSubstituteReferences()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient retired = await fixture.IngredientAsync("Retired", Unit.Ounce);
        Ingredient replacement = await fixture.IngredientAsync("Replacement", Unit.Milliliter);
        Ingredient baseIngredient = await fixture.IngredientAsync("Base", Unit.Ounce);
        Ingredient other = await fixture.IngredientAsync("Other", Unit.Ounce);
        Drink primary = await fixture.DrinkAsync(
            "Primary",
            Recipe(
                retired.Id,
                substitutes: [replacement.Id, other.Id, other.Id, retired.Id]));
        Drink substituteOnly = await fixture.DrinkAsync(
            "Substitute only",
            Recipe(
                baseIngredient.Id,
                substitutes: [retired.Id, replacement.Id, replacement.Id, baseIngredient.Id]));

        DispatchResult result = await fixture.DispatchAsync(Deleted(retired, replacement, 0.5));

        RecipeIngredient rewrittenPrimary = Assert.Single((await fixture.GetAsync(primary.Id)).Recipe.Ingredients);
        Assert.Equal(replacement.Id, rewrittenPrimary.IngredientId);
        Assert.Equal(Unit.Milliliter, rewrittenPrimary.Amount.Unit);
        Assert.Equal(14.78675, rewrittenPrimary.Amount.Value, 5);
        Assert.Equal([other.Id], rewrittenPrimary.Substitutes);
        Assert.Equal(DrinkStatus.Active, (await fixture.GetAsync(primary.Id)).Status);
        RecipeIngredient rewrittenSubstitute = Assert.Single(
            (await fixture.GetAsync(substituteOnly.Id)).Recipe.Ingredients);
        Assert.Equal(baseIngredient.Id, rewrittenSubstitute.IngredientId);
        Assert.Equal([replacement.Id], rewrittenSubstitute.Substitutes);
        Assert.Equal([primary.EntityUid, substituteOnly.EntityUid], result.Touches);
    }

    [Fact]
    public async Task IncompatibleReplacementFailsDuringPrepareBeforeHandleOrTrackedMutation()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient retired = await fixture.IngredientAsync("Retired", Unit.Ounce);
        Ingredient replacement = await fixture.IngredientAsync("Replacement", Unit.Milliliter);
        Drink compatible = await fixture.DrinkAsync("A compatible", Recipe(retired.Id));
        Drink incompatible = await fixture.DrinkAsync(
            "B incompatible",
            new Recipe(
                [new RecipeIngredient(retired.Id, Amount.Create(1, Unit.Piece))],
                ["Serve"]));
        IngredientDeleted domainEvent = Deleted(retired, replacement, 1);

        CapturedDispatch captured = await fixture.DispatchCapturingAsync(domainEvent);

        InternalError error = Assert.IsType<InternalError>(captured.Error);
        Assert.Equal($"rewrite drink {incompatible.Id} replacement amount", error.Message);
        Assert.IsType<InvalidError>(error.InnerException);
        Assert.False(captured.HandleEntered);
        Assert.DoesNotContain(captured.Entries, entry =>
            entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        Assert.Equal(retired.Id, Assert.Single((await fixture.GetAsync(compatible.Id)).Recipe.Ingredients).IngredientId);
        Assert.Equal(retired.Id, Assert.Single((await fixture.GetAsync(incompatible.Id)).Recipe.Ingredients).IngredientId);
    }

    private static Recipe Recipe(
        IngredientId primary,
        bool optional = false,
        IReadOnlyList<IngredientId>? substitutes = null) => new(
            [new RecipeIngredient(primary, Amount.Create(1, Unit.Ounce), optional, substitutes)],
            ["Mix"]);

    private static IngredientDeleted Deleted(
        Ingredient retired,
        Ingredient? replacement = null,
        double ratio = 0)
    {
        DateTimeOffset deletedAt = new(2026, 8, 9, 22, 0, 0, TimeSpan.Zero);
        return new IngredientDeleted(retired with { DeletedAt = deletedAt }, deletedAt, replacement, ratio);
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
            Drinks = services.GetRequiredService<DrinksModule>();
            Ingredients = services.GetRequiredService<IngredientsModule>();
            Queries = services.GetRequiredService<DrinkQueries>();
        }

        public MixologyStore Store { get; }
        public DrinksModule Drinks { get; }
        public IngredientsModule Ingredients { get; }
        public DrinkQueries Queries { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mixology-drink-handler-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddMixologyPersistence(Path.Combine(root, "mixology.db"), typeof(MigrationAssemblyMarker).Assembly);
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
            await using StoreSession session = await fixture.Store.OpenSessionAsync();
            await session.Context.Database.EnsureCreatedAsync();
            return fixture;
        }

        public Task<Ingredient> IngredientAsync(string name, Unit unit) => Ingredients.CreateAsync(
            Session(),
            new CreateIngredientRequest(name, IngredientCategory.Other, unit));

        public Task<Drink> DrinkAsync(string name, Recipe recipe) => Drinks.CreateAsync(
            Session(),
            new CreateDrinkRequest(name, DrinkCategory.Cocktail, GlassType.Coupe, recipe));

        public Task<Drink> GetAsync(DrinkId id) => Drinks.GetAsync(Session(), id);

        public async Task<DispatchResult> DispatchAsync(
            object domainEvent,
            Func<EventHandlerContext, Task>? afterPrepare = null)
        {
            HandlerDispatcher dispatcher = new(Queries, afterPrepare, capture: false);
            await using StoreSession session = await Store.OpenSessionAsync();
            await session.BeginWriteAsync();
            OperationContext operation = new(Actor.Owner, session);
            try
            {
                await new DispatchEventsMiddleware(dispatcher).InvokeAsync(
                    operation,
                    Operation.Command("test.ingredient-event"),
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

        public async Task<CapturedDispatch> DispatchCapturingAsync(object domainEvent)
        {
            HandlerDispatcher dispatcher = new(Queries, afterPrepare: null, capture: true);
            await using StoreSession session = await Store.OpenSessionAsync();
            await session.BeginWriteAsync();
            OperationContext operation = new(Actor.Owner, session);
            await new DispatchEventsMiddleware(dispatcher).InvokeAsync(
                operation,
                Operation.Command("test.ingredient-event"),
                context =>
                {
                    context.AddEvent(domainEvent);
                    return Task.CompletedTask;
                });
            EntityEntry[] entries = session.Context.ChangeTracker.Entries().ToArray();
            await session.RollbackAsync();
            return new CapturedDispatch(dispatcher.Error, dispatcher.HandleEntered, entries);
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

    private sealed class HandlerDispatcher(
        DrinkQueries queries,
        Func<EventHandlerContext, Task>? afterPrepare,
        bool capture) : IDomainEventDispatcher
    {
        public Exception? Error { get; private set; }

        public bool HandleEntered { get; private set; }

        public async Task DispatchAsync(EventHandlerContext context, object domainEvent)
        {
            try
            {
                switch (domainEvent)
                {
                    case IngredientUpdated updated:
                        await new IngredientUpdatedHandler(queries).HandleAsync(context, updated);
                        break;
                    case IngredientDeleted deleted:
                        IngredientDeletedHandler handler = new(queries);
                        await handler.PrepareAsync(context, deleted);
                        if (afterPrepare is not null)
                        {
                            await afterPrepare(context);
                        }

                        HandleEntered = true;
                        await handler.HandleAsync(context, deleted);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected event {domainEvent.GetType().Name}.");
                }
            }
            catch (Exception exception) when (capture)
            {
                Error = exception;
            }
        }
    }

    private sealed record DispatchResult(IReadOnlyList<EntityUid> Touches, int EventCount);

    private sealed record CapturedDispatch(
        Exception? Error,
        bool HandleEntered,
        IReadOnlyList<EntityEntry> Entries);
}
