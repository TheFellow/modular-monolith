using Cedar.Types;
using Mixology.Application.Presentation.Actions;
using Mixology.Gui.Workspaces.Audit;
using Mixology.Gui.Workspaces.Tags;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Audit.Models;
using Mixology.Modules.Audit.Presentation;
using Mixology.Modules.Audit.Requests;
using Mixology.Modules.Tagging.Models;
using Mixology.Modules.Tagging.Presentation;
using Xunit;

namespace Mixology.Gui.Tests;

public sealed class AuditTagsViewModelTests
{
    [Fact]
    public async Task AuditKeepsSelectionAndBuildsTypedEntityHistoryRequest()
    {
        AuditEntry first = Entry("create", [IngredientId.New().EntityUid]);
        AuditEntry second = Entry("update", [DrinkId.New().EntityUid, MenuId.New().EntityUid]);
        FakeAudit operations = new([first, second]);
        await using AuditViewModel viewModel = new(operations);

        await viewModel.ActivateAsync();
        viewModel.Selected = viewModel.Rows[1];
        await viewModel.RefreshAsync();

        Assert.Equal(second.Id.Value, viewModel.Selected?.Id);
        Assert.Contains(second.Touches[0].Id, viewModel.Selected?.Touches, StringComparison.Ordinal);

        IngredientId ingredient = IngredientId.New();
        viewModel.Scope = AuditScope.EntityHistory;
        viewModel.Entity = ingredient.Value;
        viewModel.Action = "Mixology::Ingredient::Action::\"update\"";
        viewModel.Expression = "success";
        viewModel.PageSize = "7";
        await viewModel.ApplyFilterAsync();

        ListAuditEntriesRequest request = operations.Requests[^1];
        Assert.Equal(ingredient.EntityUid, request.Entity);
        Assert.True(request.Action.IsEmpty);
        Assert.Equal(7, request.Limit);
        Assert.Equal("success", request.Filter);
    }

    [Fact]
    public async Task AuditRejectsInvalidInputAsTypedErrorAndNormalizesUnknownFailures()
    {
        FakeAudit operations = new([]);
        await using AuditViewModel viewModel = new(operations)
        {
            Scope = AuditScope.ActorActivity,
            PageSize = "zero",
        };

        await viewModel.ApplyFilterAsync();
        Assert.IsType<InvalidError>(viewModel.Error);
        Assert.Equal("page size must be greater than zero", viewModel.StatusMessage);

        InvalidOperationException cause = new("database secret");
        operations.List = (_, _) => Task.FromException<Page<AuditEntry>>(cause);
        await viewModel.RefreshAsync();
        InternalError normalized = Assert.IsType<InternalError>(viewModel.Error);
        Assert.Same(cause, normalized.InnerException);
        Assert.Equal("internal error", viewModel.StatusMessage);
    }

    [Fact]
    public async Task AuditSupersededResponseCannotReplaceCurrentPage()
    {
        TaskCompletionSource<Page<AuditEntry>> stale = Source<Page<AuditEntry>>();
        TaskCompletionSource<Page<AuditEntry>> current = Source<Page<AuditEntry>>();
        int calls = 0;
        FakeAudit operations = new([])
        {
            List = (_, _) => ++calls == 1 ? stale.Task : current.Task,
        };
        await using AuditViewModel viewModel = new(operations);

        Task first = viewModel.ActivateAsync();
        Task second = viewModel.RefreshAsync();
        AuditEntry currentEntry = Entry("current", []);
        current.SetResult(new Page<AuditEntry>([currentEntry], default));
        await second;
        stale.SetResult(new Page<AuditEntry>([Entry("stale", [])], default));
        await first;

        Assert.Equal(currentEntry.Id.Value, Assert.Single(viewModel.Rows).Id);
    }

    [Fact]
    public async Task TagsPreserveCaseAndKeepDiscoverySeparateFromTargetAuthorization()
    {
        IngredientId ingredient = IngredientId.New();
        TagTargetViewModel target = new(
            ingredient.EntityUid,
            "Tonic",
            "mixer",
            null!);
        FakeTags operations = new([target]);
        await using TagsViewModel viewModel = new(operations);

        await viewModel.ActivateAsync();
        viewModel.SelectedType = viewModel.TargetTypes[1];
        await viewModel.LoadTargetsCommand.ExecuteAsync(null);
        await viewModel.DrainAsync();
        viewModel.Operation = TagOperation.Add;
        viewModel.Value = "Region=West";
        await viewModel.ExecuteCommand.ExecuteAsync(null);

        Assert.Equal(new Tag("Region", "West"), operations.LastTag);
        Assert.Equal("Region=West", Assert.Single(viewModel.ResultTags));
        Assert.True(viewModel.Changed);
    }

    [Fact]
    public async Task TagsHideUnauthorizedDiscoveryAndPreserveTypedErrors()
    {
        FakeTags operations = new([]) { AllowDiscovery = false };
        await using TagsViewModel viewModel = new(operations);
        await viewModel.ActivateAsync();

        viewModel.Operation = TagOperation.Summary;
        Assert.False(viewModel.ExecuteCommand.CanExecute(null));
        Assert.DoesNotContain(TagOperation.Summary, viewModel.VisibleOperations);
        Assert.DoesNotContain(TagOperation.ShowExact, viewModel.VisibleOperations);

        PermissionError denied = AppError.Permission("tag discovery denied");
        operations.DiscoveryError = denied;
        await viewModel.RefreshAsync();
        Assert.Same(denied, viewModel.Error);
        Assert.Equal("tag discovery denied", viewModel.StatusMessage);
    }

    private static AuditEntry Entry(string action, IReadOnlyList<Mixology.Kernel.Entities.EntityUid> touches)
    {
        DateTimeOffset started = DateTimeOffset.Parse(
            "2026-08-09T12:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture);
        return new AuditEntry(
            AuditEntryId.New(),
            action,
            touches.Count == 0 ? null : touches[0],
            Mixology.Application.Authentication.Actor.Owner,
            started,
            started.AddMilliseconds(12),
            true,
            null,
            null,
            touches);
    }

    private static TaskCompletionSource<T> Source<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class FakeAudit(IReadOnlyList<AuditEntry> entries) : IAuditDesktopOperations
    {
        public List<ListAuditEntriesRequest> Requests { get; } = [];

        public Func<ListAuditEntriesRequest, CancellationToken, Task<Page<AuditEntry>>>? List { get; set; }

        public Task<Page<AuditEntry>> ListAsync(
            ListAuditEntriesRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return List?.Invoke(request, cancellationToken)
                ?? Task.FromResult(new Page<AuditEntry>(entries, default));
        }

        public Task<IReadOnlyList<ActionState>> ProjectAsync(
            AuditEntry selected,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ActionState>>(
                [new(AuditActionProjector.ViewAction, true, true)]);
    }

    private sealed class FakeTags(IReadOnlyList<TagTargetViewModel> targets) : ITagsDesktopOperations
    {
        public bool AllowDiscovery { get; set; } = true;
        public Exception? DiscoveryError { get; set; }
        public Tag LastTag { get; private set; }

        public Task<IReadOnlyList<ActionState>> ProjectDiscoveryAsync(CancellationToken cancellationToken)
        {
            if (DiscoveryError is not null)
            {
                return Task.FromException<IReadOnlyList<ActionState>>(DiscoveryError);
            }

            return Task.FromResult<IReadOnlyList<ActionState>>(
            [
                new(TaggingActionProjector.ShowAction, AllowDiscovery, AllowDiscovery),
                new(TaggingActionProjector.SummaryAction, AllowDiscovery, AllowDiscovery),
            ]);
        }

        public Task<IReadOnlyList<ActionState>> ProjectTargetAsync(
            TagTargetViewModel target,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ActionState>>(
            [
                new(TaggingActionProjector.InspectAction, true, true),
                new(TaggingActionProjector.TagAction, true, true),
                new(TaggingActionProjector.UntagAction, true, true),
            ]);

        public Task<IReadOnlyList<TagTargetViewModel>> ListTargetsAsync(
            string entityType,
            CancellationToken cancellationToken) => Task.FromResult(targets);

        public Task<TagCollection> InspectAsync(
            Mixology.Kernel.Entities.EntityUid target,
            CancellationToken cancellationToken) => Task.FromResult(TagCollection.Empty);

        public Task<TagMutationResult> UpsertAsync(
            Mixology.Kernel.Entities.EntityUid target,
            Tag value,
            CancellationToken cancellationToken)
        {
            LastTag = value;
            return Task.FromResult(new TagMutationResult(target, new TagCollection([value]), true));
        }

        public Task<TagMutationResult> RemoveAsync(
            Mixology.Kernel.Entities.EntityUid target,
            string key,
            CancellationToken cancellationToken) => Task.FromResult(
                new TagMutationResult(target, TagCollection.Empty, true));

        public Task<IReadOnlyList<TagReference>> ShowAsync(
            Tag value,
            bool exact,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TagReference>>([]);

        public Task<IReadOnlyList<TagSummary>> SummaryAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TagSummary>>([]);
    }
}
