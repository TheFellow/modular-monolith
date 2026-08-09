using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Inventory;
using Mixology.Modules.Menus.Events;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Ports;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders;
using Mixology.Modules.Tagging;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Menus.Tests;

public sealed class MenusModuleTests
{
    [Fact]
    public async Task CrudAndItemCompositionPersistAndEmitDomainEvents()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        DrinkId drink = fixture.Operations.AddDrink("Martini", Availability.Limited);
        Menu created = await fixture.Menus.CreateAsync(manager, new CreateMenuRequest("  Dinner  ", " Evening "));
        Menu updated = await fixture.Menus.UpdateAsync(
            manager,
            new UpdateMenuRequest(created.Id, "Dinner Service", "Nightly"));
        Menu composed = await fixture.Menus.AddDrinkAsync(
            manager,
            new AddMenuItemRequest(created.Id, drink));
        Menu loaded = await fixture.Menus.GetAsync(fixture.Session(Actor.Anonymous), created.Id);
        Menu emptied = await fixture.Menus.RemoveDrinkAsync(
            manager,
            new RemoveMenuItemRequest(created.Id, drink));
        Menu deleted = await fixture.Menus.DeleteAsync(manager, created.Id);

        Assert.Equal("Dinner", created.Name);
        Assert.Equal("Dinner Service", updated.Name);
        MenuItem item = Assert.Single(composed.Items);
        Assert.Equal(Availability.Limited, item.Availability);
        Assert.Null(item.DisplayName);
        Assert.Null(item.Price);
        Assert.False(item.Featured);
        Assert.Single(loaded.Items);
        Assert.Empty(emptied.Items);
        Assert.Equal(MenuStatus.Archived, deleted.Status);
        Assert.Equal(fixture.Now, deleted.DeletedAt);
        await Assert.ThrowsAsync<NotFoundError>(() => fixture.Menus.GetAsync(manager, created.Id));
        Assert.Collection(
            fixture.Dispatcher.Events,
            value => Assert.Equal(created, Assert.IsType<MenuCreated>(value).Menu),
            value =>
            {
                DrinkAddedToMenu added = Assert.IsType<DrinkAddedToMenu>(value);
                Assert.Equal(composed, added.Menu);
                Assert.Equal(item, added.Item);
            },
            value =>
            {
                DrinkRemovedFromMenu removed = Assert.IsType<DrinkRemovedFromMenu>(value);
                Assert.Equal(emptied, removed.Menu);
                Assert.Equal(item, removed.Item);
            });
    }

    [Fact]
    public async Task PublishRequiresReadinessAndRecomputesAvailabilityThenDrafts()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        DrinkId drink = fixture.Operations.AddDrink("Gimlet", Availability.Available);
        Menu menu = await fixture.Menus.CreateAsync(manager, new CreateMenuRequest("Service"));
        menu = await fixture.Menus.AddDrinkAsync(manager, new AddMenuItemRequest(menu.Id, drink));
        fixture.Operations.Findings =
        [
            new ReadinessFinding(
                ReadinessSeverity.Blocker,
                ReadinessCode.Unavailable,
                drink,
                null,
                "Gimlet is unavailable"),
        ];

        await Assert.ThrowsAsync<FailedPreconditionError>(() => fixture.Menus.PublishAsync(manager, menu.Id));
        fixture.Operations.Findings = [];
        fixture.Operations.Availability[drink] = Availability.Limited;
        Menu published = await fixture.Menus.PublishAsync(manager, menu.Id);
        Menu drafted = await fixture.Menus.DraftAsync(manager, menu.Id);

        Assert.Equal(MenuStatus.Published, published.Status);
        Assert.Equal(fixture.Now, published.PublishedAt);
        Assert.Equal(Availability.Limited, Assert.Single(published.Items).Availability);
        Assert.Equal(MenuStatus.Draft, drafted.Status);
        Assert.Null(drafted.PublishedAt);
        await Assert.ThrowsAsync<FailedPreconditionError>(() => fixture.Menus.DraftAsync(manager, menu.Id));
        MenuPublished publishedEvent = Assert.Single(fixture.Dispatcher.Events.OfType<MenuPublished>());
        MenuDrafted draftedEvent = Assert.Single(fixture.Dispatcher.Events.OfType<MenuDrafted>());
        Assert.Equal(published, publishedEvent.Menu);
        Assert.Equal(drafted, draftedEvent.Menu);
    }

    [Fact]
    public async Task ListFilterPagingAndCountAreStableAndPublic()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        await fixture.Menus.CreateAsync(manager, new CreateMenuRequest("Summer Dinner", "Patio"));
        await fixture.Menus.CreateAsync(manager, new CreateMenuRequest("Winter Dinner", "Dining room"));
        await fixture.Menus.CreateAsync(manager, new CreateMenuRequest("Summer Lunch", "Patio"));
        ListMenusRequest request = new(Filter: "name.contains(\"Summer\")", Limit: 1);

        Page<Menu> first = await fixture.Menus.ListAsync(fixture.Session(Actor.Anonymous), request);
        Page<Menu> second = await fixture.Menus.ListAsync(
            fixture.Session(Actor.Anonymous),
            request with { Cursor = first.Next });

        Assert.Single(first.Items);
        Assert.False(first.Next.IsEmpty);
        Assert.Single(second.Items);
        Assert.True(second.Next.IsEmpty);
        Assert.Equal(2, await fixture.Menus.CountAsync(fixture.Session(Actor.Anonymous), request));
        Assert.All(first.Items.Concat(second.Items), item =>
            Assert.Contains("Summer", item.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AuthorizationAndOperationalPortsRemainExplicitAndTyped()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<PermissionError>(() => fixture.Menus.CreateAsync(
            fixture.Session(Actor.Bartender),
            new CreateMenuRequest("Denied")));
        Menu menu = await fixture.Menus.CreateAsync(
            fixture.Session(Actor.Manager),
            new CreateMenuRequest("Operations"));

        await Assert.ThrowsAsync<PermissionError>(() => fixture.Menus.ReadinessAsync(
            fixture.Session(Actor.Anonymous),
            menu.Id));
        ReadinessReport report = await fixture.Menus.ReadinessAsync(
            fixture.Session(Actor.Manager),
            menu.Id);
        MenuAnalysis analysis = await fixture.Menus.AnalyzeAsync(
            fixture.Session(Actor.Manager),
            menu.Id,
            0.7);

        Assert.Equal(menu.Id, report.MenuId);
        Assert.Equal(menu.Id, analysis.Menu.Id);
        Assert.Equal(0.7, fixture.Operations.LastTargetMargin);
    }

    [Fact]
    public async Task CommandsPreserveGoRequestSemanticsAndConcreteErrorKinds()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        Menu menu = await fixture.Menus.CreateAsync(
            manager,
            new CreateMenuRequest("Parity", "Keep this description"));
        Menu updated = await fixture.Menus.UpdateAsync(
            manager,
            new UpdateMenuRequest(menu.Id, "Parity renamed", "   "));
        DrinkId first = fixture.Operations.AddDrink("First", Availability.Available);
        DrinkId second = fixture.Operations.AddDrink("Second", Availability.Available);
        DrinkId third = fixture.Operations.AddDrink("Third", Availability.Available);
        await fixture.Menus.AddDrinkAsync(manager, new AddMenuItemRequest(menu.Id, first));
        await fixture.Menus.AddDrinkAsync(manager, new AddMenuItemRequest(menu.Id, second));
        Menu composed = await fixture.Menus.AddDrinkAsync(manager, new AddMenuItemRequest(menu.Id, third));

        InvalidError duplicate = await Assert.ThrowsAsync<InvalidError>(() => fixture.Menus.AddDrinkAsync(
            manager,
            new AddMenuItemRequest(menu.Id, first)));
        NotFoundError absent = await Assert.ThrowsAsync<NotFoundError>(() => fixture.Menus.RemoveDrinkAsync(
            manager,
            new RemoveMenuItemRequest(menu.Id, DrinkId.New())));
        NotFoundError unknownDrink = await Assert.ThrowsAsync<NotFoundError>(() => fixture.Menus.AddDrinkAsync(
            manager,
            new AddMenuItemRequest(menu.Id, DrinkId.New())));
        Menu removed = await fixture.Menus.RemoveDrinkAsync(
            manager,
            new RemoveMenuItemRequest(menu.Id, second));

        Assert.Equal("Keep this description", updated.Description);
        Assert.Contains("already on menu", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains("not on menu", absent.Message, StringComparison.Ordinal);
        Assert.Contains("drink", unknownDrink.Message, StringComparison.Ordinal);
        Assert.Equal([0, 1, 2], composed.Items.Select(static item => item.SortOrder));
        Assert.Equal([0, 2], removed.Items.Select(static item => item.SortOrder));
        Assert.Equal(2, (await fixture.Menus.GetAsync(manager, menu.Id)).Items.Count);

        await fixture.Menus.CreateAsync(manager, new CreateMenuRequest("Unique"));
        await Assert.ThrowsAsync<ConflictError>(() => fixture.Menus.CreateAsync(
            manager,
            new CreateMenuRequest("Unique")));
    }

    [Fact]
    public async Task AvailabilityFailuresDegradeToUnavailableForAddAndPublish()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        DrinkId drink = fixture.Operations.AddDrink("Dependency failure", Availability.Available);
        fixture.Operations.AvailabilityFailures.Add(drink);
        Menu menu = await fixture.Menus.CreateAsync(manager, new CreateMenuRequest("Fallback"));
        menu = await fixture.Menus.AddDrinkAsync(manager, new AddMenuItemRequest(menu.Id, drink));

        Assert.Equal(Availability.Unavailable, Assert.Single(menu.Items).Availability);
        Menu published = await fixture.Menus.PublishAsync(manager, menu.Id);
        Assert.Equal(Availability.Unavailable, Assert.Single(published.Items).Availability);
    }

    [Fact]
    public async Task AvailabilityDoesNotDegradeWrappedCancellation()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        DrinkId drink = fixture.Operations.AddDrink("Cancelled dependency", Availability.Available);
        AggregateException cancellation = new(new TaskCanceledException("cancelled"));
        fixture.Operations.AvailabilityExceptions[drink] = cancellation;
        Menu menu = await fixture.Menus.CreateAsync(manager, new CreateMenuRequest("Cancellation"));

        AggregateException thrown = await Assert.ThrowsAsync<AggregateException>(() =>
            fixture.Menus.AddDrinkAsync(manager, new AddMenuItemRequest(menu.Id, drink)));

        Assert.Same(cancellation, thrown);
        Assert.True(AppError.IsCancellation(thrown));
    }

    [Fact]
    public async Task EveryPersistedMenuFilterFieldMatchesTheGoSchema()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        Menu target = await fixture.Menus.CreateAsync(
            manager,
            new CreateMenuRequest("Summer Terrace", "Seasonal patio menu"));
        fixture.AdvanceClock(TimeSpan.FromSeconds(1));
        await fixture.Menus.CreateAsync(manager, new CreateMenuRequest("Winter Cellar", "Rich winter menu"));
        string instant = target.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        string[] filters =
        [
            $"id == \"{target.Id}\"",
            "name.contains(\"Summer\")",
            "description.contains(\"patio\")",
            "status == \"draft\" && name.contains(\"Summer\")",
            $"created_at == date(\"{instant}\")",
        ];

        foreach (string filter in filters)
        {
            Page<Menu> page = await fixture.Menus.ListAsync(
                fixture.Session(Actor.Anonymous),
                new ListMenusRequest(Filter: filter));
            Assert.Equal(target.Id, Assert.Single(page.Items).Id);
        }
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        private Fixture(
            string root,
            ServiceProvider services,
            DateTimeOffset now,
            FixedTimeProvider clock,
            FakeMenuOperations operations,
            RecordingDispatcher dispatcher)
        {
            this.root = root;
            this.services = services;
            Now = now;
            Clock = clock;
            Operations = operations;
            Dispatcher = dispatcher;
            Menus = services.GetRequiredService<MenusModule>();
        }

        public DateTimeOffset Now { get; }
        private FixedTimeProvider Clock { get; }
        public FakeMenuOperations Operations { get; }
        public RecordingDispatcher Dispatcher { get; }
        public MenusModule Menus { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-menus-tests", Guid.NewGuid().ToString("N"));
            string database = Path.Combine(root, "mixology.db");
            Directory.CreateDirectory(root);
            DateTimeOffset now = new(2026, 8, 9, 22, 0, 0, TimeSpan.Zero);
            FixedTimeProvider clock = new(now);
            FakeMenuOperations operations = new();
            RecordingDispatcher dispatcher = new();
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddSingleton<TimeProvider>(clock);
            collection.AddSingleton<IDomainEventDispatcher>(dispatcher);
            collection.AddMixologyPersistence(database, typeof(MigrationAssemblyMarker).Assembly);
            collection.AddMixologyApplication();
            collection.AddAuditModule();
            collection.AddIngredientsModule();
            collection.AddInventoryModule();
            collection.AddDrinksModule();
            collection.AddMenusModule();
            collection.AddOrdersModule();
            collection.AddTaggingModule();
            collection.Replace(ServiceDescriptor.Singleton<IMenuOperations>(operations));
            ServiceProvider services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            await services.GetRequiredService<MixologyStore>().InitializeAsync();
            return new Fixture(root, services, now, clock, operations, dispatcher);
        }

        public MixologySession Session(Actor actor) =>
            services.GetRequiredService<MixologySessionFactory>().Create(actor);

        public void AdvanceClock(TimeSpan duration) => Clock.Advance(duration);

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

    public sealed class FakeMenuOperations : IMenuOperations
    {
        public Dictionary<DrinkId, MenuDrink> Drinks { get; } = [];
        public Dictionary<DrinkId, Availability> Availability { get; } = [];
        public HashSet<DrinkId> AvailabilityFailures { get; } = [];
        public Dictionary<DrinkId, Exception> AvailabilityExceptions { get; } = [];
        public IReadOnlyList<ReadinessFinding> Findings { get; set; } = [];
        public double? LastTargetMargin { get; private set; }

        public DrinkId AddDrink(string name, Availability availability)
        {
            DrinkId id = DrinkId.New();
            Drinks[id] = new MenuDrink(id, name);
            Availability[id] = availability;
            return id;
        }

        public ValueTask<MenuDrink> GetDrinkAsync(
            StoreSession session,
            DrinkId id,
            CancellationToken cancellationToken = default) =>
            Drinks.TryGetValue(id, out MenuDrink? drink)
                ? ValueTask.FromResult(drink)
                : ValueTask.FromException<MenuDrink>(AppError.NotFound($"drink {id} not found"));

        public ValueTask<Availability> GetAvailabilityAsync(
            StoreSession session,
            DrinkId id,
            CancellationToken cancellationToken = default) =>
            AvailabilityExceptions.TryGetValue(id, out Exception? exception)
                ? ValueTask.FromException<Availability>(exception)
                : AvailabilityFailures.Contains(id)
                ? ValueTask.FromException<Availability>(AppError.Internal("availability dependency failed"))
                : Availability.TryGetValue(id, out Availability availability)
                ? ValueTask.FromResult(availability)
                : ValueTask.FromException<Availability>(AppError.NotFound($"drink {id} not found"));

        public ValueTask<ReadinessReport> GetReadinessAsync(
            StoreSession session,
            Menu menu,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ReadinessReport(menu.Id, menu.Status, Findings));

        public ValueTask<MenuAnalysis> AnalyzeAsync(
            StoreSession session,
            Menu menu,
            double targetMargin,
            CancellationToken cancellationToken = default)
        {
            LastTargetMargin = targetMargin;
            return ValueTask.FromResult(new MenuAnalysis(menu, [], 0, menu.Items.Count, null));
        }

        public ValueTask<IReadOnlyList<IngredientFulfillment>?> FulfillIngredientsAsync(
            StoreSession session,
            IReadOnlyList<Mixology.Modules.Drinks.Models.RecipeIngredient> requirements,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<IngredientFulfillment>?>([]);
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
        private DateTimeOffset current = now;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current += duration;
    }
}
