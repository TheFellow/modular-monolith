using Cedar.Types;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Authorization.Cedar;
using Mixology.Dispatcher;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Tags;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Authorization;
using Mixology.Modules.Ingredients.Events;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
using Mixology.Modules.Tagging;
using Mixology.Persistence;
using Mixology.Presentation;
using Mixology.Presentation.Mutations;
using Xunit;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Presentation.Tests;

public sealed class TaggedMutationCoordinatorTests
{
    [Fact]
    public async Task DomainMutationAndCompleteTagReplacementCommitTogether()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.CreateIngredientAsync("Lime");
        TagCollection desired = new([new Tag("origin", "mexico"), new Tag("seasonal")]);

        Ingredient updated = await fixture.Coordinator.RunAsync(
            fixture.Manager,
            (session, token) => fixture.Ingredients.UpdateAsync(
                session,
                new UpdateIngredientRequest(ingredient.Id, Name: "Key Lime"),
                token),
            desired,
            static value => value.EntityUid,
            static (value, tags) => value with { Tags = tags });

        Ingredient persisted = await fixture.Ingredients.GetAsync(fixture.Manager, ingredient.Id);
        Assert.Equal("Key Lime", updated.Name);
        Assert.Equal(desired.Strings(), updated.Tags.Strings());
        Assert.Equal("Key Lime", persisted.Name);
        Assert.Equal(desired.Strings(), persisted.Tags.Strings());
        Assert.Equal(3, await fixture.AuditCountAsync());
        Assert.Contains(fixture.Events, static value => value is IngredientUpdated);
    }

    [Fact]
    public async Task PostStateTagDenialRollsBackDomainEventEffectsAndAudit()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.CreateIngredientAsync("Lime");
        int auditBefore = await fixture.AuditCountAsync();

        PermissionError thrown = await Assert.ThrowsAsync<PermissionError>(() => fixture.Coordinator.RunAsync(
            fixture.Manager,
            (session, token) => fixture.Ingredients.UpdateAsync(
                session,
                new UpdateIngredientRequest(ingredient.Id, Name: "Forbidden Lime"),
                token),
            new TagCollection([new Tag("deny", "yes")]),
            static value => value.EntityUid,
            static (value, tags) => value with { Tags = tags }));

        Ingredient persisted = await fixture.Ingredients.GetAsync(fixture.Manager, ingredient.Id);
        Assert.Same(fixture.Denial, thrown);
        Assert.Equal("Lime", persisted.Name);
        Assert.Empty(persisted.Tags);
        Assert.Equal(auditBefore, await fixture.AuditCountAsync());
        Assert.Contains(fixture.Events, static value => value is IngredientUpdated);
    }

    [Fact]
    public async Task DomainFailurePreservesExactTypedErrorAndNeverStartsTagReplacement()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.CreateIngredientAsync("Lime");
        await fixture.Tagging.ReplaceAsync(
            fixture.Manager,
            ingredient.EntityUid,
            new TagCollection([new Tag("existing", "yes")]));
        ConflictError expected = AppError.Conflict("domain rejected the mutation");
        bool targetSelected = false;

        ConflictError thrown = await Assert.ThrowsAsync<ConflictError>(() => fixture.Coordinator.RunAsync(
            fixture.Manager,
            (_, _) => Task.FromException<Ingredient>(expected),
            TagCollection.Empty,
            value =>
            {
                targetSelected = true;
                return value.EntityUid;
            },
            static (value, tags) => value with { Tags = tags }));

        Assert.Same(expected, thrown);
        Assert.False(targetSelected);
        Assert.Equal(
            ["existing=yes"],
            (await fixture.Ingredients.GetAsync(fixture.Manager, ingredient.Id)).Tags.Strings());
    }

    [Fact]
    public async Task OmittedTagsPreserveExistingTagsWhileExplicitEmptyClearsThem()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.CreateIngredientAsync("Lime");
        await fixture.Tagging.ReplaceAsync(
            fixture.Manager,
            ingredient.EntityUid,
            new TagCollection([new Tag("existing", "yes")]));
        bool tagStageEntered = false;

        Ingredient preserved = await fixture.Coordinator.RunAsync(
            fixture.Manager,
            (session, token) => fixture.Ingredients.UpdateAsync(
                session,
                new UpdateIngredientRequest(ingredient.Id, Description: "fresh"),
                token),
            desiredTags: null,
            value =>
            {
                tagStageEntered = true;
                return value.EntityUid;
            },
            static (value, tags) => value with { Tags = tags });
        Ingredient cleared = await fixture.Coordinator.RunAsync(
            fixture.Manager,
            (session, token) => fixture.Ingredients.UpdateAsync(
                session,
                new UpdateIngredientRequest(ingredient.Id, Description: "freshest"),
                token),
            TagCollection.Empty,
            static value => value.EntityUid,
            static (value, tags) => value with { Tags = tags });

        Assert.False(tagStageEntered);
        Assert.Equal(["existing=yes"], preserved.Tags.Strings());
        Assert.Empty(cleared.Tags);
        Assert.Empty((await fixture.Ingredients.GetAsync(fixture.Manager, ingredient.Id)).Tags);
    }

    [Fact]
    public async Task CancellationIsNotNormalizedOrWrapped()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        OperationCanceledException expected = new("mutation cancelled");

        OperationCanceledException thrown = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Coordinator.RunAsync(
                fixture.Manager,
                (_, _) => Task.FromException<Ingredient>(expected),
                TagCollection.Empty,
                static value => value.EntityUid,
                static (value, tags) => value with { Tags = tags }));

        Assert.Same(expected, thrown);
        Assert.True(AppError.IsCancellation(thrown));
    }

    [Fact]
    public async Task UnknownMutationFailureBecomesSafeInternalErrorWithCause()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        InvalidOperationException expected = new("database password must not escape");

        InternalError thrown = await Assert.ThrowsAsync<InternalError>(() => fixture.Coordinator.RunAsync(
            fixture.Manager,
            (_, _) => Task.FromException<Ingredient>(expected),
            desiredTags: null,
            static value => value.EntityUid,
            static (value, tags) => value with { Tags = tags }));

        Assert.Same(expected, thrown.InnerException);
        Assert.Equal("internal error", thrown.UserMessage);
    }

    [Fact]
    public async Task UnknownResultMappingFailureIsWrappedAndRollsBackBothStages()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Ingredient ingredient = await fixture.CreateIngredientAsync("Lime");
        int auditBefore = await fixture.AuditCountAsync();
        InvalidOperationException expected = new("mapping implementation failed");

        InternalError thrown = await Assert.ThrowsAsync<InternalError>(() => fixture.Coordinator.RunAsync(
            fixture.Manager,
            (session, token) => fixture.Ingredients.UpdateAsync(
                session,
                new UpdateIngredientRequest(ingredient.Id, Name: "Mapped Lime"),
                token),
            new TagCollection([new Tag("mapped", "yes")]),
            static value => value.EntityUid,
            (_, _) => throw expected));

        Ingredient persisted = await fixture.Ingredients.GetAsync(fixture.Manager, ingredient.Id);
        Assert.Same(expected, thrown.InnerException);
        Assert.Equal("internal error", thrown.UserMessage);
        Assert.Equal("Lime", persisted.Name);
        Assert.Empty(persisted.Tags);
        Assert.Equal(auditBefore, await fixture.AuditCountAsync());
    }

    [Fact]
    public async Task NestedCoordinatorParticipatesInCallerTransactionWithoutCommittingIt()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        IngredientId createdId = default;
        InvalidOperationException expected = new("outer workflow rejected its continuation");

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Manager.ExecuteAtomicAsync(
                async (outerSession, token) =>
                {
                    Ingredient created = await fixture.Coordinator.RunAsync(
                        outerSession,
                        (session, innerToken) => fixture.Ingredients.CreateAsync(
                            session,
                            new CreateIngredientRequest("Outer Lime", IngredientCategory.Juice, Unit.Ounce),
                            innerToken),
                        new TagCollection([new Tag("workflow", "outer")]),
                        static value => value.EntityUid,
                        static (value, tags) => value with { Tags = tags },
                        token);
                    createdId = created.Id;
                    return created;
                },
                (_, _, _) => Task.FromException<int>(expected)));

        Assert.Same(expected, thrown);
        await Assert.ThrowsAsync<NotFoundError>(() => fixture.Ingredients.GetAsync(fixture.Manager, createdId));
        Assert.Equal(0, await fixture.AuditCountAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        private Fixture(string root, ServiceProvider services)
        {
            this.root = root;
            this.services = services;
            Coordinator = services.GetRequiredService<TaggedMutationCoordinator>();
            Ingredients = services.GetRequiredService<IngredientsModule>();
            Tagging = services.GetRequiredService<TaggingModule>();
            Manager = services.GetRequiredService<MixologySessionFactory>().Create(Actor.Manager);
            Denial = services.GetRequiredService<PostStateDenyingAuthorizer>().Denial;
            Events = services.GetRequiredService<ObservingDispatcher>().Events;
        }

        public TaggedMutationCoordinator Coordinator { get; }
        public IngredientsModule Ingredients { get; }
        public TaggingModule Tagging { get; }
        public MixologySession Manager { get; }
        public PermissionError Denial { get; }
        public IReadOnlyList<object> Events { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mixology-tagged-mutation-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<DomainEventDispatcher>();
            services.AddSingleton<ObservingDispatcher>();
            services.AddSingleton<IDomainEventDispatcher>(static provider =>
                provider.GetRequiredService<ObservingDispatcher>());
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
            services.AddMixologyPresentation();
            services.AddSingleton<CedarAuthorizer>();
            services.AddSingleton<PostStateDenyingAuthorizer>();
            services.Replace(ServiceDescriptor.Singleton<IEntityAuthorizer>(static provider =>
                provider.GetRequiredService<PostStateDenyingAuthorizer>()));
            ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            await provider.GetRequiredService<MixologyStore>().InitializeAsync();
            return new Fixture(root, provider);
        }

        public Task<Ingredient> CreateIngredientAsync(string name) => Ingredients.CreateAsync(
            Manager,
            new CreateIngredientRequest(name, IngredientCategory.Juice, Unit.Ounce));

        public async Task<int> AuditCountAsync()
        {
            await using StoreSession session = await services.GetRequiredService<MixologyStore>().OpenSessionAsync();
            await session.Context.Database.OpenConnectionAsync();
            await using System.Data.Common.DbCommand command = session.Context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM audit_entries";
            object value = await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("audit count returned null");
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
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

    private sealed class ObservingDispatcher(DomainEventDispatcher inner) : IDomainEventDispatcher
    {
        public List<object> Events { get; } = [];

        public async Task DispatchAsync(
            Mixology.Application.Operations.EventHandlerContext context,
            object domainEvent)
        {
            Events.Add(domainEvent);
            await inner.DispatchAsync(context, domainEvent);
        }
    }

    private sealed class PostStateDenyingAuthorizer(CedarAuthorizer inner) : IEntityAuthorizer
    {
        public PermissionError Denial { get; } = AppError.Permission("post-state tag policy denied mutation");

        public async ValueTask AuthorizeAsync(
            Actor principal,
            KernelEntityUid action,
            Entity resource,
            CancellationToken cancellationToken = default)
        {
            await inner.AuthorizeAsync(principal, action, resource, cancellationToken);
            if (action == IngredientAuthorization.Tag
                && resource.Tags.TryGetValue(new CedarString("deny"), out ICedarData? value)
                && value is CedarString { Value: "yes" })
            {
                throw Denial;
            }
        }
    }
}
