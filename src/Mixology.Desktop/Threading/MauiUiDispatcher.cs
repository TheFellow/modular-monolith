using Microsoft.Maui.ApplicationModel;
using Mixology.Toolkits.Desktop.Threading;

namespace Mixology.Desktop.Threading;

public sealed class MauiUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => MainThread.IsMainThread;

    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            action();
        });
    }
}
