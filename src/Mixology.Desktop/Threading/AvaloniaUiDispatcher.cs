using Avalonia.Threading;
using Mixology.Toolkits.Desktop.Threading;

namespace Mixology.Desktop.Threading;

public sealed class AvaloniaUiDispatcher(Avalonia.Threading.Dispatcher dispatcher) : IUiDispatcher
{
    public AvaloniaUiDispatcher()
        : this(Avalonia.Threading.Dispatcher.UIThread)
    {
    }

    public bool CheckAccess() => dispatcher.CheckAccess();

    public async Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        await dispatcher.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken);
    }
}
