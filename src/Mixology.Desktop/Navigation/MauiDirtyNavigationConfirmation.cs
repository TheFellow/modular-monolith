using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Mixology.Desktop.Workspaces;

namespace Mixology.Desktop.Navigation;

public sealed class MauiDirtyNavigationConfirmation : IDirtyNavigationConfirmation
{
    public Task<bool> ConfirmDiscardAsync(
        IDesktopWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        cancellationToken.ThrowIfCancellationRequested();
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Microsoft.Maui.Controls.Application.Current?.Windows is not { Count: > 0 } windows)
            {
                throw new InvalidOperationException("The desktop window is not available.");
            }

            if (windows[0].Page is not { } page)
            {
                throw new InvalidOperationException("The desktop page is not available.");
            }

            return page.DisplayAlertAsync(
                "Discard changes?",
                $"{workspace.Title} has unsaved changes. Discard them and continue?",
                "Discard",
                "Stay");
        });
    }
}
