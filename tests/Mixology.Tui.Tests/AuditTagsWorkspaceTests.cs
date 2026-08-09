using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Audit;
using Mixology.Modules.Audit.Models;
using Mixology.Modules.Audit.Presentation;
using Mixology.Modules.Audit.Requests;
using Mixology.Modules.Drinks;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
using Mixology.Modules.Tagging;
using Mixology.Modules.Tagging.Models;
using Mixology.Modules.Tagging.Presentation;
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces;
using Mixology.Tui.Workspaces.Audit;
using Mixology.Tui.Workspaces.Tags;
using Xunit;

namespace Mixology.Tui.Tests;

public sealed class AuditTagsWorkspaceTests
{
    [Fact]
    public async Task AuditKeepsStableSelectionAcrossRefreshAndRendersExactTouches()
    {
        AuditEntry first = Entry("create", Actor.Owner, [IngredientId.New().EntityUid]);
        AuditEntry second = Entry("update", Actor.Manager, [DrinkId.New().EntityUid, MenuId.New().EntityUid]);
        FakeAudit operations = new([first, second]);
        await using AuditWorkspace workspace = new(operations);

        await workspace.ActivateAsync();
        _ = workspace.Handle('j');
        _ = workspace.Handle('r');
        await workspace.DrainAsync();

        Assert.Equal(second.Id, workspace.Selected?.Id);
        string rendered = workspace.Render(new Viewport(160, 21));
        Assert.Contains($"Mixology::Drink::\"{second.Touches[0].Id}\"", rendered, StringComparison.Ordinal);
        Assert.Contains($"Mixology::Menu::\"{second.Touches[1].Id}\"", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditScopeFormBuildsEntityHistoryRequestAndOwnsEscape()
    {
        FakeAudit operations = new([]);
        await using AuditWorkspace workspace = new(operations);
        await workspace.ActivateAsync();

        Assert.True(workspace.Handle('f'));
        Assert.Equal(InputOwnership.Edit, workspace.InputOwnership);
        IngredientId ingredient = IngredientId.New();
        workspace.SetField("Scope", "entity history");
        workspace.SetField("Entity", ingredient.Value);
        workspace.SetField("Action", "Mixology::Ingredient::Action::\"update\"");
        workspace.SetField("From", "2026-08-01");
        workspace.SetField("Expression", "success");
        workspace.SetField("Page size", "7");
        _ = workspace.Handle(AuditWorkspace.SubmitKey);
        await workspace.DrainAsync();

        ListAuditEntriesRequest request = operations.Requests[^1];
        Assert.Equal(AuditScope.EntityHistory, workspace.Scope);
        Assert.Equal(ingredient.EntityUid, request.Entity);
        Assert.True(request.Action.IsEmpty);
        Assert.Equal(7, request.Limit);
        Assert.Equal("success", request.Filter);
    }

    [Fact]
    public async Task AuditActorScopeRejectsInvalidPageSizeAsTypedInput()
    {
        FakeAudit operations = new([]);
        await using AuditWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        _ = workspace.Handle('f');
        workspace.SetField("Scope", "actor activity");
        workspace.SetField("Principal", "bartender");
        workspace.SetField("Page size", "zero");

        _ = workspace.Handle(AuditWorkspace.SubmitKey);

        Assert.Equal(AuditWorkspaceMode.Filter, workspace.Mode);
        Assert.Contains("page size must be greater than zero", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);
        Assert.Single(operations.Requests);

        workspace.SetField("Page size", "5");
        _ = workspace.Handle(AuditWorkspace.SubmitKey);
        await workspace.DrainAsync();

        Assert.Equal(AuditScope.ActorActivity, workspace.Scope);
        Assert.Equal(Actor.Bartender, operations.Requests[^1].Principal);
        Assert.True(operations.Requests[^1].Entity.IsEmpty);
    }

    [Fact]
    public async Task AuditRejectsSupersededDeferredListAndDisposalDrainsCancellation()
    {
        AuditEntry stale = Entry("stale", Actor.Owner, []);
        AuditEntry current = Entry("current", Actor.Owner, []);
        TaskCompletionSource<Page<AuditEntry>> first = Source<Page<AuditEntry>>();
        TaskCompletionSource<Page<AuditEntry>> second = Source<Page<AuditEntry>>();
        int call = 0;
        FakeAudit operations = new([])
        {
            List = (_, _) => ++call == 1 ? first.Task : second.Task,
        };
        AuditWorkspace workspace = new(operations);
        Task initial = workspace.ActivateAsync();
        Task refresh = workspace.RefreshAsync();
        second.SetResult(new Page<AuditEntry>([current], default));
        await refresh;
        first.SetResult(new Page<AuditEntry>([stale], default));
        await initial;

        Assert.Equal(current.Id, Assert.Single(workspace.Rows).Id);
        await workspace.DisposeAsync();
    }

    [Fact]
    public async Task TagsOperationPickerPreservesCaseSensitiveTagMutation()
    {
        IngredientId ingredient = IngredientId.New();
        TagTargetChoice choice = new(ingredient.EntityUid, "Tonic", "mixer", null!);
        FakeTags operations = new([choice]);
        await using TagsWorkspace workspace = new(operations);
        await workspace.ActivateAsync();

        _ = workspace.Handle('j'); // add
        _ = workspace.Handle('\r');
        _ = workspace.Handle('j'); // ingredients
        _ = workspace.Handle('\r');
        await workspace.DrainAsync();
        _ = workspace.Handle('\r');
        await workspace.DrainAsync();
        Assert.Equal(TagsWorkspaceMode.EnteringValue, workspace.Mode);
        Assert.Equal(InputOwnership.Edit, workspace.InputOwnership);
        workspace.SetValue("Region=West");
        _ = workspace.Handle(TagsWorkspace.SubmitKey);
        await workspace.DrainAsync();

        Assert.Equal(new Tag("Region", "West"), operations.LastTag);
        Assert.Equal(TagsWorkspaceMode.Results, workspace.Mode);
        Assert.Contains("Region=West", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TagsDiscoveryControlsAreNotDisclosedWhenUnauthorized()
    {
        FakeTags operations = new([]) { AllowDiscovery = false };
        await using TagsWorkspace workspace = new(operations);
        await workspace.ActivateAsync();

        string rendered = workspace.Render(new Viewport(80, 21));
        Assert.DoesNotContain("Show exact tag", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Show all values for key", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Tag usage summary", rendered, StringComparison.Ordinal);
        Assert.Contains("Inspect entity tags", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TagsDiscoveryDistinguishesExactKeyAndSummaryOperations()
    {
        FakeTags operations = new([]);
        await using TagsWorkspace workspace = new(operations);
        await workspace.ActivateAsync();

        MoveDown(workspace, 3);
        _ = workspace.Handle('\r');
        workspace.SetValue("Region=West");
        _ = workspace.Handle(TagsWorkspace.SubmitKey);
        await workspace.DrainAsync();
        Assert.True(operations.LastExact);
        Assert.Equal(new Tag("Region", "West"), operations.LastShown);

        _ = workspace.Handle('\u001b');
        _ = workspace.Handle('j');
        _ = workspace.Handle('\r');
        workspace.SetValue("Region");
        _ = workspace.Handle(TagsWorkspace.SubmitKey);
        await workspace.DrainAsync();
        Assert.False(operations.LastExact);
        Assert.Equal(new Tag("Region"), operations.LastShown);

        _ = workspace.Handle('\u001b');
        _ = workspace.Handle('j');
        _ = workspace.Handle('\r');
        await workspace.DrainAsync();
        Assert.Equal(1, operations.SummaryCalls);
        Assert.Contains("TOTAL", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TagsIgnoresSupersededTargetLoadAndSafeErrorsDoNotLeak()
    {
        TaskCompletionSource<IReadOnlyList<TagTargetChoice>> deferred = Source<IReadOnlyList<TagTargetChoice>>();
        FakeTags operations = new([]) { ListTargets = (_, _) => deferred.Task };
        await using TagsWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        _ = workspace.Handle('\r'); // inspect
        _ = workspace.Handle('\r'); // drinks
        Assert.Equal(TagsWorkspaceMode.Loading, workspace.Mode);
        _ = workspace.Handle('\u001b');
        deferred.SetResult([new TagTargetChoice(DrinkId.New().EntityUid, "stale", "stale", null!)]);
        await workspace.DrainAsync();

        Assert.Equal(TagsWorkspaceMode.Operations, workspace.Mode);
        Assert.Empty(workspace.Targets);
    }

    [Fact]
    public async Task RealSqliteCedarAuditAndCaseSensitiveTagFlowThroughFactories()
    {
        await using ProductionFixture fixture = await ProductionFixture.CreateAsync();
        MixologySession session = fixture.Session(Actor.Owner);
        IngredientsModule ingredients = fixture.Get<IngredientsModule>();
        Ingredient ingredient = await ingredients.CreateAsync(
            session,
            new CreateIngredientRequest("Real Tonic", IngredientCategory.Mixer, Unit.Milliliter));

        Func<ITuiWorkspace> auditFactory = AuditWorkspace.CreateFactory(
            fixture.Get<AuditModule>(),
            fixture.Get<AuditActionProjector>(),
            session,
            Actor.Owner);
        await using AuditWorkspace audit = Assert.IsType<AuditWorkspace>(auditFactory());
        await audit.ActivateAsync();
        Assert.Contains(audit.Rows, entry => entry.Touches.Contains(ingredient.EntityUid));

        Func<ITuiWorkspace> tagsFactory = TagsWorkspace.CreateFactory(
            fixture.Get<TaggingModule>(),
            fixture.Get<TaggingActionProjector>(),
            fixture.Get<DrinksModule>(),
            ingredients,
            fixture.Get<InventoryModule>(),
            fixture.Get<MenusModule>(),
            fixture.Get<OrdersModule>(),
            session,
            Actor.Owner);
        await using TagsWorkspace tags = Assert.IsType<TagsWorkspace>(tagsFactory());
        await tags.ActivateAsync();
        _ = tags.Handle('j');
        _ = tags.Handle('\r');
        _ = tags.Handle('j');
        _ = tags.Handle('\r');
        await tags.DrainAsync();
        Assert.Contains(tags.Targets, value => value.Uid == ingredient.EntityUid);
        _ = tags.Handle('\r');
        await tags.DrainAsync();
        tags.SetValue("Region=West");
        _ = tags.Handle(TagsWorkspace.SubmitKey);
        await tags.DrainAsync();

        TagCollection persisted = await fixture.Get<TaggingModule>().ListAsync(session, ingredient.EntityUid);
        Assert.Equal(["Region=West"], persisted.Strings());
    }

    private static AuditEntry Entry(string action, Actor actor, IReadOnlyList<EntityUid> touches)
    {
        DateTimeOffset now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
        EntityUid? resource = touches.Count == 0 ? null : touches[0];
        return new AuditEntry(AuditEntryId.New(), action, resource, actor, now, now, true, null, null, touches);
    }

    private static TaskCompletionSource<T> Source<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void MoveDown(TagsWorkspace workspace, int count)
    {
        for (int index = 0; index < count; index++)
        {
            _ = workspace.Handle('j');
        }
    }

    private static IReadOnlyList<ActionState> AuditActions() =>
    [
        new(AuditActionProjector.ListAction, true, true),
        new(AuditActionProjector.ViewAction, true, true),
    ];

    private sealed class FakeAudit(IReadOnlyList<AuditEntry> entries) : IAuditWorkspaceOperations
    {
        public Func<ListAuditEntriesRequest, CancellationToken, Task<Page<AuditEntry>>>? List { get; init; }
        public List<ListAuditEntriesRequest> Requests { get; } = [];

        public Task<Page<AuditEntry>> ListAsync(ListAuditEntriesRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return List?.Invoke(request, cancellationToken) ?? Task.FromResult(new Page<AuditEntry>(entries, default));
        }

        public Task<IReadOnlyList<ActionState>> ProjectAsync(AuditEntry? selected, CancellationToken cancellationToken)
        {
            _ = selected;
            _ = cancellationToken;
            return Task.FromResult(AuditActions());
        }
    }

    private sealed class FakeTags(IReadOnlyList<TagTargetChoice> targets) : ITagsWorkspaceOperations
    {
        public bool AllowDiscovery { get; init; } = true;
        public Func<string, CancellationToken, Task<IReadOnlyList<TagTargetChoice>>>? ListTargets { get; init; }
        public Tag? LastTag { get; private set; }
        public Tag? LastShown { get; private set; }
        public bool? LastExact { get; private set; }
        public int SummaryCalls { get; private set; }

        public Task<IReadOnlyList<ActionState>> ProjectDiscoveryAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            IReadOnlyList<ActionState> states =
            [
                new(TaggingActionProjector.ShowAction, AllowDiscovery, AllowDiscovery),
                new(TaggingActionProjector.SummaryAction, AllowDiscovery, AllowDiscovery),
            ];
            return Task.FromResult(states);
        }

        public Task<IReadOnlyList<ActionState>> ProjectTargetAsync(TagTargetChoice target, CancellationToken cancellationToken)
        {
            _ = target;
            _ = cancellationToken;
            IReadOnlyList<ActionState> states =
            [
                new(TaggingActionProjector.InspectAction, true, true),
                new(TaggingActionProjector.TagAction, true, true),
                new(TaggingActionProjector.UntagAction, true, true),
            ];
            return Task.FromResult(states);
        }

        public Task<IReadOnlyList<TagTargetChoice>> ListTargetsAsync(string entityType, CancellationToken cancellationToken) =>
            ListTargets?.Invoke(entityType, cancellationToken) ?? Task.FromResult(targets);

        public Task<TagCollection> InspectAsync(EntityUid target, CancellationToken cancellationToken)
        {
            _ = target;
            _ = cancellationToken;
            return Task.FromResult(new TagCollection([]));
        }

        public Task<TagMutationResult> UpsertAsync(EntityUid target, Tag value, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            LastTag = value;
            return Task.FromResult(new TagMutationResult(target, new TagCollection([value]), true));
        }

        public Task<TagMutationResult> RemoveAsync(EntityUid target, string key, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            LastTag = new Tag(key);
            return Task.FromResult(new TagMutationResult(target, new TagCollection([]), true));
        }

        public Task<IReadOnlyList<TagReference>> ShowAsync(Tag value, bool exact, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            LastShown = value;
            LastExact = exact;
            return Task.FromResult<IReadOnlyList<TagReference>>([]);
        }

        public Task<IReadOnlyList<TagSummary>> SummaryAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            SummaryCalls++;
            return Task.FromResult<IReadOnlyList<TagSummary>>([]);
        }
    }

    private sealed class ProductionFixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly TuiHost host;

        private ProductionFixture(string root, TuiHost host)
        {
            this.root = root;
            this.host = host;
        }

        public static async Task<ProductionFixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-tui-audit-tags", Guid.NewGuid().ToString("N"));
            TuiOptions options = TuiOptions.Create(
                Path.Combine(root, "mixology.db"),
                "owner",
                "error",
                "text",
                Path.Combine(root, "mixology.log"),
                metrics: false);
            return new ProductionFixture(root, await TuiHost.OpenAsync(options));
        }

        public T Get<T>() where T : notnull => host.Services.GetRequiredService<T>();
        public MixologySession Session(Actor actor) => Get<MixologySessionFactory>().Create(actor);

        public async ValueTask DisposeAsync()
        {
            await host.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
