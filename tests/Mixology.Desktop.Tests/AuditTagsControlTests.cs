using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Mixology.Application.Presentation.Actions;
using Mixology.Desktop.Workspaces.Audit;
using Mixology.Desktop.Workspaces.Tags;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Audit.Models;
using Mixology.Modules.Audit.Presentation;
using Mixology.Modules.Audit.Requests;
using Mixology.Modules.Tagging.Models;
using Mixology.Modules.Tagging.Presentation;
using Xunit;

namespace Mixology.Desktop.Tests;

public sealed class AuditTagsControlTests
{
    [AvaloniaFact]
    public async Task AuditViewUsesSemanticScrollableQueryAndDetailControls()
    {
        await using AuditViewModel viewModel = new(new AuditOperations());
        AuditView view = new() { DataContext = viewModel };
        Window window = new() { Content = view };

        window.Show();
        Assert.Contains(window.GetVisualDescendants().OfType<ComboBox>(), combo =>
            ReferenceEquals(combo.ItemsSource, viewModel.Scopes));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBox>(), text =>
            text.PlaceholderText?.Contains("Typed filter", StringComparison.Ordinal) == true);
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), button =>
            Equals(button.Content, "Apply filter") && button.Command is not null);
        Assert.Contains(window.GetVisualDescendants().OfType<ScrollViewer>(), _ => true);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TagsViewUsesSemanticOperationTargetAndCaseSensitiveValueControls()
    {
        await using TagsViewModel viewModel = new(new TagsOperations());
        TagsView view = new() { DataContext = viewModel };
        Window window = new() { Content = view };

        window.Show();
        Assert.True(window.GetVisualDescendants().OfType<ComboBox>().Count() >= 3);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBox>(), text =>
            text.PlaceholderText?.Contains("case-sensitive", StringComparison.Ordinal) == true);
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), button =>
            Equals(button.Content, "Run") && button.Command is not null);
        Assert.Contains(window.GetVisualDescendants().OfType<ScrollViewer>(), _ => true);
        window.Close();
    }

    private sealed class AuditOperations : IAuditDesktopOperations
    {
        public Task<Page<AuditEntry>> ListAsync(
            ListAuditEntriesRequest request,
            CancellationToken cancellationToken) => Task.FromResult(new Page<AuditEntry>([], default));

        public Task<IReadOnlyList<ActionState>> ProjectAsync(
            AuditEntry selected,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ActionState>>(
                [new(AuditActionProjector.ViewAction, true, true)]);
    }

    private sealed class TagsOperations : ITagsDesktopOperations
    {
        public Task<IReadOnlyList<ActionState>> ProjectDiscoveryAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActionState>>(
            [
                new(TaggingActionProjector.ShowAction, true, true),
                new(TaggingActionProjector.SummaryAction, true, true),
            ]);

        public Task<IReadOnlyList<ActionState>> ProjectTargetAsync(
            TagTargetViewModel target,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ActionState>>([]);

        public Task<IReadOnlyList<TagTargetViewModel>> ListTargetsAsync(
            string entityType,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TagTargetViewModel>>([]);

        public Task<TagCollection> InspectAsync(EntityUid target, CancellationToken cancellationToken) =>
            Task.FromResult(TagCollection.Empty);

        public Task<TagMutationResult> UpsertAsync(
            EntityUid target,
            Tag value,
            CancellationToken cancellationToken) => Task.FromResult(new TagMutationResult(target, TagCollection.Empty, false));

        public Task<TagMutationResult> RemoveAsync(
            EntityUid target,
            string key,
            CancellationToken cancellationToken) => Task.FromResult(new TagMutationResult(target, TagCollection.Empty, false));

        public Task<IReadOnlyList<TagReference>> ShowAsync(
            Tag value,
            bool exact,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TagReference>>([]);

        public Task<IReadOnlyList<TagSummary>> SummaryAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TagSummary>>([]);
    }
}
