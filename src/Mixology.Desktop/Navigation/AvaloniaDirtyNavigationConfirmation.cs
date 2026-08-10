using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Mixology.Desktop.Workspaces;

namespace Mixology.Desktop.Navigation;

public sealed class AvaloniaDirtyNavigationConfirmation : IDirtyNavigationConfirmation
{
    private readonly Func<Window?> owner;
    private readonly Func<string, DirtyNavigationDialog> createDialog;

    public AvaloniaDirtyNavigationConfirmation(
        Func<Window?>? owner = null,
        Func<string, DirtyNavigationDialog>? createDialog = null)
    {
        this.owner = owner ?? ResolveMainWindow;
        this.createDialog = createDialog ?? (title => new DirtyNavigationDialog(title));
    }

    public async Task<bool> ConfirmDiscardAsync(
        IDesktopWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        cancellationToken.ThrowIfCancellationRequested();
        Window? dialogOwner = owner();
        if (dialogOwner is null)
        {
            return false;
        }

        DirtyNavigationDialog dialog = createDialog(workspace.Title);
        Task<bool> answer = dialog.ShowDialog<bool>(dialogOwner);
        using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (dialog.IsVisible)
                {
                    dialog.Close(false);
                }
            }));
        try
        {
            bool confirmed = await answer.ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            return confirmed;
        }
        finally
        {
            if (dialog.IsVisible)
            {
                dialog.Close(false);
            }
        }
    }

    private static Window? ResolveMainWindow() =>
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}

public sealed class DirtyNavigationDialog : Window
{
    public DirtyNavigationDialog(string workspaceTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceTitle);
        Title = "Discard changes?";
        CanResize = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        TextBlock prompt = new()
        {
            Text = $"Discard unsaved changes in {workspaceTitle}?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 420,
        };
        Button keepEditing = new()
        {
            Content = "Keep editing",
            IsCancel = true,
            MinWidth = 110,
        };
        AutomationProperties.SetName(keepEditing, "Keep editing");
        keepEditing.Click += (_, _) => Close(false);
        Button discard = new()
        {
            Content = "Discard changes",
            IsDefault = true,
            MinWidth = 130,
        };
        AutomationProperties.SetName(discard, "Discard changes");
        discard.Click += (_, _) => Close(true);

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = "Unsaved changes",
                    FontSize = 20,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                },
                prompt,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { keepEditing, discard },
                },
            },
        };
    }
}
