using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Operations;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Migrations;
using Mixology.Modules.Audit.Models;
using Mixology.Modules.Audit.Requests;
using Mixology.Modules.Drinks;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Inventory;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Audit.Tests;

public sealed class AuditModuleTests
{
    private static readonly EntityUid ActionOne = new("Test::Action", "one");
    private static readonly EntityUid ActionTwo = new("Test::Action", "two");

    [Fact]
    public async Task OwnerCanPageCountAndUsePurposeBuiltHistoryQueries()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        EntityUid target = new(EntityIds.IngredientType, IngredientId.New().Value);
        EntityUid other = new(EntityIds.DrinkType, DrinkId.New().Value);
        await fixture.RecordAsync(Actor.Manager, ActionOne, target, [target]);
        await fixture.RecordAsync(Actor.Manager, ActionTwo, other, [other]);
        await fixture.RecordAsync(Actor.Owner, ActionOne, other, [target]);
        MixologySession owner = fixture.Session(Actor.Owner);

        ListAuditEntriesRequest request = new(Limit: 1);
        Page<AuditEntry> first = await fixture.Module.ListAsync(owner, request);
        Page<AuditEntry> second = await fixture.Module.ListAsync(owner, request with { Cursor = first.Next });
        Page<AuditEntry> third = await fixture.Module.ListAsync(owner, request with { Cursor = second.Next });
        AuditEntry[] paged = [.. first.Items, .. second.Items, .. third.Items];

        Assert.Equal(3, paged.Length);
        Assert.Equal(3, paged.Select(static entry => entry.Id).Distinct().Count());
        Assert.False(first.Next.IsEmpty);
        Assert.False(second.Next.IsEmpty);
        Assert.True(third.Next.IsEmpty);
        Assert.Equal(3, await fixture.Module.CountAsync(owner, request));
        Assert.Equal(2, (await fixture.Module.GetEntityHistoryAsync(owner, target)).Items.Count);
        Assert.Equal(2, (await fixture.Module.GetActorActivityAsync(owner, Actor.Manager)).Items.Count);
    }

    [Fact]
    public async Task StructuredAndExpressionFiltersRunBeforeAuthorization()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        EntityUid target = new(EntityIds.IngredientType, IngredientId.New().Value);
        EntityUid other = new(EntityIds.DrinkType, DrinkId.New().Value);
        await fixture.RecordAsync(Actor.Manager, ActionOne, target, [target]);
        await fixture.RecordAsync(Actor.Owner, ActionTwo, other, [other]);
        RecordingAuthorizer recording = new();
        AuditModule module = new(fixture.Store, recording);

        Page<AuditEntry> page = await module.ListAsync(
            fixture.Session(Actor.Owner),
            new ListAuditEntriesRequest(
                Action: ActionOne,
                Principal: Actor.Manager,
                Entity: target,
                Filter: "success && action.contains(\"one\")"));

        AuditEntry entry = Assert.Single(page.Items);
        Assert.Equal(CedarName(ActionOne), entry.Action);
        Assert.Equal(target, entry.Resource);
        Assert.Equal(Actor.Manager, entry.Principal);
        Assert.Equal([entry.Id.Value], recording.ResourceIds);
    }

    [Fact]
    public async Task DeniedActorsObserveNoRowsCountsHistoryOrActivity()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        EntityUid target = new(EntityIds.IngredientType, IngredientId.New().Value);
        await fixture.RecordAsync(Actor.Manager, ActionOne, target, [target]);
        MixologySession manager = fixture.Session(Actor.Manager);

        Assert.Empty((await fixture.Module.ListAsync(manager, new ListAuditEntriesRequest())).Items);
        Assert.Equal(0, await fixture.Module.CountAsync(manager, new ListAuditEntriesRequest()));
        Assert.Empty((await fixture.Module.GetEntityHistoryAsync(manager, target)).Items);
        Assert.Empty((await fixture.Module.GetActorActivityAsync(manager, Actor.Manager)).Items);
    }

    [Fact]
    public async Task InvalidRequestsRemainPreciselyTyped()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession owner = fixture.Session(Actor.Owner);

        await Assert.ThrowsAsync<InvalidError>(() => fixture.Module.ListAsync(
            owner,
            new ListAuditEntriesRequest(Cursor: "not-an-audit-id")));
        await Assert.ThrowsAsync<InvalidError>(() => fixture.Module.ListAsync(
            owner,
            new ListAuditEntriesRequest(Action: new EntityUid("Test::Action", string.Empty))));
        await Assert.ThrowsAsync<InvalidError>(() => fixture.Module.ListAsync(
            owner,
            new ListAuditEntriesRequest(
                From: DateTimeOffset.UnixEpoch.AddDays(1),
                To: DateTimeOffset.UnixEpoch)));
        await Assert.ThrowsAsync<InvalidError>(() => fixture.Module.ListAsync(
            owner,
            new ListAuditEntriesRequest(Filter: "missing == true")));
    }

    private static string CedarName(EntityUid uid) => uid.ToCedarUid().MarshalCedar();

    private sealed class RecordingAuthorizer : IEntityAuthorizer
    {
        public List<string> ResourceIds { get; } = [];

        public ValueTask AuthorizeAsync(
            Actor principal,
            EntityUid action,
            Cedar.Types.Entity resource,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResourceIds.Add(resource.Uid.Id.Value);
            return ValueTask.CompletedTask;
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
            Module = services.GetRequiredService<AuditModule>();
        }

        public MixologyStore Store { get; }
        public AuditModule Module { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-audit-read-tests", Guid.NewGuid().ToString("N"));
            ServiceCollection collection = new();
            collection.AddLogging();
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
            ServiceProvider services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            Fixture fixture = new(root, services);
            await fixture.Store.InitializeAsync();
            return fixture;
        }

        public MixologySession Session(Actor actor) =>
            services.GetRequiredService<MixologySessionFactory>().Create(actor);

        public Task RecordAsync(
            Actor actor,
            EntityUid action,
            EntityUid resource,
            IReadOnlyList<EntityUid> touches) =>
            Session(actor).ExecuteAsync(Operation.Command(CedarName(action)), context =>
            {
                context.SelectResource(resource);
                foreach (EntityUid touch in touches)
                {
                    context.Touch(touch);
                }

                return Task.CompletedTask;
            });

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
