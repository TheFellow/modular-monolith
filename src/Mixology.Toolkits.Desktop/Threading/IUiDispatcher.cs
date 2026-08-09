namespace Mixology.Toolkits.Desktop.Threading;

/// <summary>Owns publication of view-model state onto a desktop UI thread.</summary>
public interface IUiDispatcher
{
    bool CheckAccess();

    Task InvokeAsync(Action action, CancellationToken cancellationToken = default);
}

/// <summary>A synchronous dispatcher for view-model tests and non-UI consumers.</summary>
public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;

    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        action();
        return Task.CompletedTask;
    }
}
