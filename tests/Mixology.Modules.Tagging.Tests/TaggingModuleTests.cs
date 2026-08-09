using Cedar.Types;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Application;
using Mixology.Application.Auditing;
using Mixology.Application.Authentication;
using Mixology.Application.Operations;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Tags;
using Mixology.Modules.Tagging.Authorization;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence;
using Xunit;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Tagging.Tests;

public sealed class TaggingModuleTests
{
    [Fact]
    public async Task UpsertSetAndRemoveAreCaseSensitiveAndIdempotent()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        KernelEntityUid target = fixture.AddMenu("Terrace");

        TagMutationResult added = await fixture.Tagging.UpsertAsync(
            fixture.Owner,
            target,
            new Tag("region", "west"));
        TagMutationResult unchanged = await fixture.Tagging.SetAsync(
            fixture.Owner,
            target,
            new Tag("region", "west"));
        TagMutationResult distinctCase = await fixture.Tagging.UpsertAsync(
            fixture.Owner,
            target,
            new Tag("Region", "east"));
        TagMutationResult updated = await fixture.Tagging.UpsertAsync(
            fixture.Owner,
            target,
            new Tag("region", "north"));
        TagMutationResult absent = await fixture.Tagging.RemoveAsync(fixture.Owner, target, "missing");
        TagMutationResult removed = await fixture.Tagging.RemoveAsync(fixture.Owner, target, "Region");

        Assert.True(added.Changed);
        Assert.False(unchanged.Changed);
        Assert.True(distinctCase.Changed);
        Assert.True(updated.Changed);
        Assert.False(absent.Changed);
        Assert.True(removed.Changed);
        Tag remaining = Assert.Single(await fixture.Tagging.ListAsync(fixture.Anonymous, target));
        Assert.Equal(new Tag("region", "north"), remaining);
    }

    [Fact]
    public async Task ReplaceIsAtomicAndRoutesTagAndUntagPermissionsIndependently()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        KernelEntityUid target = fixture.AddMenu("Dinner");
        await fixture.Tagging.ReplaceAsync(
            fixture.Owner,
            target,
            new TagCollection([new Tag("a", "1"), new Tag("b", "2")]));

        TagMutationResult addition = await fixture.Tagging.ReplaceAsync(
            fixture.Manager,
            target,
            new TagCollection([new Tag("a", "1"), new Tag("b", "2"), new Tag("c", "3")]));
        PermissionError denied = await Assert.ThrowsAsync<PermissionError>(() =>
            fixture.Tagging.ReplaceAsync(
                fixture.Manager,
                target,
                new TagCollection([new Tag("a", "changed"), new Tag("c", "3")])));
        TagMutationResult unchanged = await fixture.Tagging.ReplaceAsync(
            fixture.Manager,
            target,
            addition.Tags);
        TagMutationResult pureRemoval = await fixture.Tagging.ReplaceAsync(
            fixture.Bartender,
            target,
            new TagCollection([new Tag("a", "1"), new Tag("c", "3")]));

        Assert.True(addition.Changed);
        Assert.Contains("untag", denied.Message, StringComparison.Ordinal);
        Assert.False(unchanged.Changed);
        Assert.True(pureRemoval.Changed);
        Assert.Equal(
            ["a=1", "c=3"],
            (await fixture.Tagging.ListAsync(fixture.Anonymous, target)).Strings());
    }

    [Fact]
    public async Task ListLoadsOwningResourceAndOverlaysCurrentPersistedTagsForAuthorization()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        KernelEntityUid target = fixture.AddMenu("Lunch");
        await fixture.Tagging.UpsertAsync(fixture.Owner, target, new Tag("audience", "public"));

        TagCollection tags = await fixture.Tagging.ListAsync(fixture.Anonymous, target);

        Assert.Equal(["audience=public"], tags.Strings());
        Assert.Equal(2, fixture.Targets[target].Loads);
    }

    [Fact]
    public async Task ShowAndSummaryUseBulkActiveIdsAndRetainButHideInactiveAssociations()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        KernelEntityUid menu = fixture.AddMenu("Brunch");
        KernelEntityUid secondMenu = fixture.AddMenu("Supper");
        KernelEntityUid ingredient = fixture.AddIngredient("Lemon");
        KernelEntityUid inactive = fixture.AddIngredient("Retired lime");
        await fixture.Tagging.UpsertAsync(fixture.Owner, menu, new Tag("season", "summer"));
        await fixture.Tagging.UpsertAsync(fixture.Owner, secondMenu, new Tag("season", "winter"));
        await fixture.Tagging.UpsertAsync(fixture.Owner, ingredient, new Tag("season", "summer"));
        await fixture.Tagging.UpsertAsync(fixture.Owner, inactive, new Tag("season", "summer"));
        fixture.Targets[inactive].Active = false;
        fixture.ResetActiveCalls();

        IReadOnlyList<TagReference> wide = await fixture.Tagging.ShowAsync(
            fixture.Owner,
            new Tag("season", "ignored"),
            exact: false);
        IReadOnlyList<TagReference> exact = await fixture.Tagging.ShowAsync(
            fixture.Owner,
            new Tag("season", "summer"),
            exact: true);
        IReadOnlyList<TagSummary> summary = await fixture.Tagging.SummaryAsync(fixture.Owner);

        Assert.Equal(3, wide.Count);
        Assert.Equal(["Brunch", "Lemon"], exact.Select(value => value.EntityName).Order());
        TagSummary season = Assert.Single(summary, value => value.Tag == "season=summer");
        Assert.Equal("season=summer", season.Tag);
        Assert.Equal(2, season.Total);
        Assert.Equal(1, season.Ingredients);
        Assert.Equal(1, season.Menus);
        Assert.Equal(3, fixture.ActiveCalls[EntityIds.MenuType]);
        Assert.Equal(3, fixture.ActiveCalls[EntityIds.IngredientType]);
        Assert.Equal(["season=summer"], (await fixture.RepositoryTags(inactive)).Strings());
    }

    [Fact]
    public async Task DiscoveryIsOwnerOnlyAndDoesNotAuthorizeReferencedResources()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        KernelEntityUid target = fixture.AddMenu("Private operations name");
        await fixture.Tagging.UpsertAsync(fixture.Owner, target, new Tag("ops", "true"));

        await Assert.ThrowsAsync<PermissionError>(() => fixture.Tagging.ShowAsync(
            fixture.Manager,
            new Tag("ops", "true"),
            exact: true));
        await Assert.ThrowsAsync<PermissionError>(() => fixture.Tagging.SummaryAsync(fixture.Anonymous));
        TagReference reference = Assert.Single(await fixture.Tagging.ShowAsync(
            fixture.Owner,
            new Tag("ops", "true"),
            exact: true));

        Assert.Equal("Private operations name", reference.EntityName);
    }

    [Fact]
    public async Task InvalidTargetsAndLoaderFailuresPreserveConcreteTypedErrors()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        KernelEntityUid missing = MenuId.New().EntityUid;
        KernelEntityUid mismatch = new(EntityIds.MenuType, IngredientId.New().Value);
        KernelEntityUid audit = AuditEntryId.New().EntityUid;
        NotFoundError expected = AppError.NotFound("domain-owned target missing");
        fixture.Failures[missing] = expected;

        NotFoundError actual = await Assert.ThrowsAsync<NotFoundError>(() => fixture.Tagging.UpsertAsync(
            fixture.Owner,
            missing,
            new Tag("a", "b")));
        InvalidError mismatched = await Assert.ThrowsAsync<InvalidError>(() => fixture.Tagging.ListAsync(
            fixture.Owner,
            mismatch));
        InvalidError unsupported = await Assert.ThrowsAsync<InvalidError>(() => fixture.Tagging.ListAsync(
            fixture.Owner,
            audit));

        Assert.Same(expected, actual);
        Assert.True(AppError.IsNotFound(actual));
        Assert.Contains("invalid tag target", mismatched.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported tag target", unsupported.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoaderContractViolationsAreInternalErrors()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        KernelEntityUid wrongUid = fixture.AddMenu("Wrong UID");
        KernelEntityUid unnamed = fixture.AddMenu("Unnamed");
        fixture.Targets[wrongUid].ReturnedUid = MenuId.New().EntityUid;
        fixture.Targets[unnamed].Name = "   ";

        InternalError wrong = await Assert.ThrowsAsync<InternalError>(() => fixture.Tagging.ListAsync(
            fixture.Owner,
            wrongUid));
        InternalError empty = await Assert.ThrowsAsync<InternalError>(() => fixture.Tagging.ListAsync(
            fixture.Owner,
            unnamed));

        Assert.Contains("returned", wrong.Message, StringComparison.Ordinal);
        Assert.Contains("empty display name", empty.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrappedTypedLoaderErrorsRemainUnchangedAndClassifiable()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        KernelEntityUid target = MenuId.New().EntityUid;
        InternalError expected = AppError.Internal(
            "domain target lookup failed",
            AppError.NotFound("wrapped target absence"));
        fixture.Failures[target] = expected;

        InternalError actual = await Assert.ThrowsAsync<InternalError>(() => fixture.Tagging.ListAsync(
            fixture.Owner,
            target));

        Assert.Same(expected, actual);
        Assert.True(AppError.IsInternal(actual));
        Assert.True(AppError.IsNotFound(actual));
    }

    [Fact]
    public async Task PostStateAuthorizationDenialRollsBackTheTagMutation()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        KernelEntityUid target = fixture.AddMenu("Protected");

        await Assert.ThrowsAsync<PermissionError>(() => fixture.Tagging.UpsertAsync(
            fixture.Manager,
            target,
            new Tag("blocked", "true")));

        Assert.Empty(await fixture.Tagging.ListAsync(fixture.Anonymous, target));
    }

    [Fact]
    public void RegistryRejectsIncompleteAndDuplicateDomainOwnership()
    {
        TagTargetRegistry registry = new();
        TagTargetRegistration valid = Fixture.Registration(EntityIds.MenuType, new(), new(), new());

        registry.Register(valid);

        Assert.Throws<InvalidOperationException>(() => registry.Register(valid));
        Assert.Throws<InvalidOperationException>(() => registry.Register(valid with
        {
            EntityType = string.Empty,
        }));
        Assert.Throws<InvalidError>(() => registry.Resolve(EntityIds.AuditEntryType));
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        private Fixture(string root, ServiceProvider services)
        {
            this.root = root;
            this.services = services;
            Tagging = services.GetRequiredService<TaggingModule>();
            Owner = Session(Actor.Owner);
            Manager = Session(Actor.Manager);
            Bartender = Session(Actor.Bartender);
            Anonymous = Session(Actor.Anonymous);
        }

        public Dictionary<KernelEntityUid, FakeTarget> Targets { get; } = [];
        public Dictionary<KernelEntityUid, Exception> Failures { get; } = [];
        public Dictionary<string, int> ActiveCalls { get; } = new(StringComparer.Ordinal);
        public TaggingModule Tagging { get; }
        public MixologySession Owner { get; }
        public MixologySession Manager { get; }
        public MixologySession Bartender { get; }
        public MixologySession Anonymous { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-tagging-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddMixologyPersistence(Path.Combine(root, "mixology.db"), typeof(TaggingModule).Assembly);
            collection.AddMixologyApplication();
            collection.Replace(ServiceDescriptor.Singleton<IActivityRecorder, NoOpActivityRecorder>());
            collection.AddTaggingModule();
            collection.TryAddEnumerable(ServiceDescriptor.Singleton<ICedarAuthorizationModule, TestTargetsCedarModule>());
            ServiceProvider services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            await using (StoreSession session = await services.GetRequiredService<MixologyStore>().OpenSessionAsync())
            {
                await session.Context.Database.EnsureCreatedAsync();
            }

            Fixture fixture = new(root, services);
            TagTargetRegistry registry = services.GetRequiredService<TagTargetRegistry>();
            registry.Register(Registration(EntityIds.MenuType, fixture.Targets, fixture.Failures, fixture.ActiveCalls));
            registry.Register(Registration(
                EntityIds.IngredientType,
                fixture.Targets,
                fixture.Failures,
                fixture.ActiveCalls));
            return fixture;
        }

        public KernelEntityUid AddMenu(string name)
        {
            KernelEntityUid target = MenuId.New().EntityUid;
            Targets[target] = new FakeTarget(target, name);
            return target;
        }

        public KernelEntityUid AddIngredient(string name)
        {
            KernelEntityUid target = IngredientId.New().EntityUid;
            Targets[target] = new FakeTarget(target, name);
            return target;
        }

        public void ResetActiveCalls() => ActiveCalls.Clear();

        public async Task<TagCollection> RepositoryTags(KernelEntityUid target)
        {
            await using StoreSession session = await services.GetRequiredService<MixologyStore>().OpenSessionAsync();
            return await services.GetRequiredService<TagRepository>().ListAsync(session, target);
        }

        public static TagTargetRegistration Registration(
            string entityType,
            Dictionary<KernelEntityUid, FakeTarget> targets,
            Dictionary<KernelEntityUid, Exception> failures,
            Dictionary<string, int> activeCalls)
        {
            string domain = entityType[(entityType.LastIndexOf("::", StringComparison.Ordinal) + 2)..];
            string actionType = $"Mixology::{domain}::Action";
            return new TagTargetRegistration(
                entityType,
                new KernelEntityUid(actionType, "get"),
                new KernelEntityUid(actionType, "tag"),
                new KernelEntityUid(actionType, "untag"),
                (_, id, _) =>
                {
                    KernelEntityUid uid = new(entityType, id);
                    if (failures.TryGetValue(uid, out Exception? failure))
                    {
                        return ValueTask.FromException<TagTargetState>(failure);
                    }

                    if (!targets.TryGetValue(uid, out FakeTarget? target))
                    {
                        return ValueTask.FromException<TagTargetState>(
                            AppError.NotFound($"{domain.ToLowerInvariant()} {id} not found"));
                    }

                    target.Loads++;
                    Dictionary<CedarString, ICedarData> attributes = new()
                    {
                        [new CedarString("Name")] = new CedarString(target.Name),
                        [new CedarString("Status")] = new CedarString(target.Active ? "active" : "retired"),
                    };
                    Entity entity = new(
                        target.ReturnedUid.ToCedarUid(),
                        new EntityUidSet(),
                        new CedarRecord(attributes),
                        new CedarRecord());
                    return ValueTask.FromResult(new TagTargetState(entity, target.Name));
                },
                (_, ids, _) =>
                {
                    activeCalls[entityType] = activeCalls.GetValueOrDefault(entityType) + 1;
                    HashSet<string> active = ids.Where(id =>
                            targets.TryGetValue(new KernelEntityUid(entityType, id), out FakeTarget? target) &&
                            target.Active)
                        .ToHashSet(StringComparer.Ordinal);
                    return ValueTask.FromResult<IReadOnlySet<string>>(active);
                });
        }

        private MixologySession Session(Actor actor) =>
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

    internal sealed class FakeTarget(KernelEntityUid uid, string name)
    {
        public KernelEntityUid ReturnedUid { get; set; } = uid;
        public string Name { get; set; } = name;
        public bool Active { get; set; } = true;
        public int Loads { get; set; }
    }

    private sealed class NoOpActivityRecorder : IActivityRecorder
    {
        public Task RecordAsync(OperationContext context, OperationActivity activity)
        {
            _ = context;
            _ = activity;
            return Task.CompletedTask;
        }
    }

    private sealed class TestTargetsCedarModule : ICedarAuthorizationModule
    {
        public string SchemaName => "Mixology.Modules.Tagging.Tests/targets.cedarschema";
        public string SchemaText => Schema;
        public IReadOnlyCollection<string> ResourceTypes => [EntityIds.MenuType, EntityIds.IngredientType];
        public IReadOnlyList<CedarPolicyDocument> Policies =>
        [
            new("Mixology.Modules.Tagging.Tests/targets.cedar", Policy),
        ];

        private const string Schema = """
            namespace Mixology {
                entity Actor enum ["owner", "manager", "sommelier", "bartender", "anonymous"];
                entity Menu { Name: String, Status: String } tags String;
                entity Ingredient { Name: String, Status: String } tags String;
            }

            namespace Mixology::Menu {
                action get, tag, untag appliesTo {
                    principal: Mixology::Actor,
                    resource: Mixology::Menu,
                    context: {}
                };
            }

            namespace Mixology::Ingredient {
                action get, tag, untag appliesTo {
                    principal: Mixology::Actor,
                    resource: Mixology::Ingredient,
                    context: {}
                };
            }
            """;

        private const string Policy = """
            permit(principal, action in [
                Mixology::Menu::Action::"get",
                Mixology::Ingredient::Action::"get"
            ], resource);

            permit(principal == Mixology::Actor::"manager", action in [
                Mixology::Menu::Action::"tag",
                Mixology::Ingredient::Action::"tag"
            ], resource);

            permit(principal == Mixology::Actor::"bartender", action in [
                Mixology::Menu::Action::"untag",
                Mixology::Ingredient::Action::"untag"
            ], resource);

            forbid(
                principal == Mixology::Actor::"manager",
                action in [Mixology::Menu::Action::"tag", Mixology::Ingredient::Action::"tag"],
                resource
            ) when { resource.hasTag("blocked") };
            """;
    }
}
