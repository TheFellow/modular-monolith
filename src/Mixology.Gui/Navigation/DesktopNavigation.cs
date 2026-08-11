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
        IconSource = Icon(item.Id);
        OpenCommand = new AsyncRelayCommand(
            token => open(this, token),
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public WorkspaceId Id { get; }

    public string Label { get; }

    public string IconSource { get; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public IAsyncRelayCommand OpenCommand { get; }

    private static string Icon(WorkspaceId id) => id.Value switch
    {
        "dashboard" => "icon_dashboard.svg",
        "drinks" => "icon_drinks.svg",
        "ingredients" => "icon_ingredients.svg",
        "inventory" => "icon_inventory.svg",
        "menus" => "icon_menus.svg",
        "orders" => "icon_orders.svg",
        "audit" => "icon_audit.svg",
        "tags" => "icon_tags.svg",
        _ => "icon_dashboard.svg",
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
