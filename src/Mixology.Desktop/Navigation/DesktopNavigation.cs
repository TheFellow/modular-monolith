using CommunityToolkit.Mvvm.Input;
using Mixology.Presentation.Navigation;

namespace Mixology.Desktop.Navigation;

public sealed class DesktopNavigationItemViewModel
{
    public DesktopNavigationItemViewModel(
        NavigationItem item,
        Func<DesktopNavigationItemViewModel, CancellationToken, Task> open)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(open);
        Id = item.Id;
        Label = item.Label;
        OpenCommand = new AsyncRelayCommand(
            token => open(this, token),
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public WorkspaceId Id { get; }

    public string Label { get; }

    public IAsyncRelayCommand OpenCommand { get; }
}

public interface IDirtyNavigationConfirmation
{
    Task<bool> ConfirmDiscardAsync(
        Workspaces.IDesktopWorkspace workspace,
        CancellationToken cancellationToken = default);
}

public sealed class RejectDirtyNavigationConfirmation : IDirtyNavigationConfirmation
{
    public Task<bool> ConfirmDiscardAsync(
        Workspaces.IDesktopWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
