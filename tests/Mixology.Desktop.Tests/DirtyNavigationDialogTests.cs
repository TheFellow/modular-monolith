using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using Mixology.Desktop.Navigation;
using Mixology.Desktop.Workspaces;
using Mixology.Presentation.Navigation;
using Xunit;

namespace Mixology.Desktop.Tests;

public sealed class DirtyNavigationDialogTests
{
    [AvaloniaFact]
    public async Task DiscardUsesAnOwnedModalAndReturnsTrue()
    {
        Window owner = new() { Title = "Owner" };
        DirtyNavigationDialog? created = null;
        AvaloniaDirtyNavigationConfirmation confirmation = new(
            () => owner,
            title => created = new DirtyNavigationDialog(title));
        owner.Show();
        try
        {
            Task<bool> pending = confirmation.ConfirmDiscardAsync(new DirtyWorkspace());
            DirtyNavigationDialog dialog = Assert.IsType<DirtyNavigationDialog>(created);
            Assert.Same(owner, dialog.Owner);
            Button discard = Assert.Single(
                dialog.GetVisualDescendants().OfType<Button>(),
                button => Equals(button.Content, "Discard changes"));

            discard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.True(await pending);
            Assert.False(dialog.IsVisible);
        }
        finally
        {
            owner.Close();
        }
    }

    [AvaloniaFact]
    public async Task KeepEditingReturnsFalse()
    {
        Window owner = new();
        DirtyNavigationDialog? created = null;
        AvaloniaDirtyNavigationConfirmation confirmation = new(
            () => owner,
            title => created = new DirtyNavigationDialog(title));
        owner.Show();
        try
        {
            Task<bool> pending = confirmation.ConfirmDiscardAsync(new DirtyWorkspace());
            DirtyNavigationDialog dialog = Assert.IsType<DirtyNavigationDialog>(created);
            Button keepEditing = Assert.Single(
                dialog.GetVisualDescendants().OfType<Button>(),
                button => Equals(button.Content, "Keep editing"));

            keepEditing.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.False(await pending);
        }
        finally
        {
            owner.Close();
        }
    }

    [AvaloniaFact]
    public async Task CancellationClosesTheModalAndRemainsCancellation()
    {
        Window owner = new();
        DirtyNavigationDialog? created = null;
        AvaloniaDirtyNavigationConfirmation confirmation = new(
            () => owner,
            title => created = new DirtyNavigationDialog(title));
        using CancellationTokenSource cancellation = new();
        owner.Show();
        try
        {
            Task<bool> pending = confirmation.ConfirmDiscardAsync(
                new DirtyWorkspace(),
                cancellation.Token);
            DirtyNavigationDialog dialog = Assert.IsType<DirtyNavigationDialog>(created);

            await cancellation.CancelAsync();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.False(dialog.IsVisible);
        }
        finally
        {
            owner.Close();
        }
    }

    private sealed class DirtyWorkspace : ObservableObject, IDesktopWorkspace
    {
        public WorkspaceId Id => new("dirty");
        public string Title => "Recipe editor";
        public bool IsDirty => true;
        public Task ActivateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
