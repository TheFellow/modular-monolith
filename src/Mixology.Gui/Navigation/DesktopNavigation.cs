using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mixology.Presentation.Navigation;

namespace Mixology.Gui.Navigation;

public sealed partial class DesktopNavigationItemViewModel : ObservableObject
{
    public DesktopNavigationItemViewModel(
        NavigationItem item,
        Func<DesktopNavigationItemViewModel, CancellationToken, Task> open)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(open);
        Id = item.Id;
        Label = item.Label;
        DisplayLabel = $"{Glyph(item.Id)}   {item.Label}";
        OpenCommand = new AsyncRelayCommand(
            token => open(this, token),
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public WorkspaceId Id { get; }

    public string Label { get; }

    public string DisplayLabel { get; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public IAsyncRelayCommand OpenCommand { get; }

    private static string Glyph(WorkspaceId id) => id.Value switch
    {
        "dashboard" => "▦",
        "drinks" => "◈",
        "ingredients" => "◆",
        "inventory" => "▤",
        "menus" => "▧",
        "orders" => "▣",
        "audit" => "▥",
        "tags" => "◇",
        _ => "•",
    };
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
