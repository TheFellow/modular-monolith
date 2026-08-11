using Mixology.Kernel.Errors;
using Mixology.Persistence;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces;

namespace Mixology.Tui;

public sealed class TuiShell : IAsyncDisposable
{
    public const int MinimumWidth = 80;
    public const int MinimumHeight = 24;

    private readonly IReadOnlyDictionary<WorkspaceId, Func<ITuiWorkspace>> factories;
    private readonly object sync = new();
    private readonly HashSet<WorkspaceId> visible;
    private readonly Dictionary<WorkspaceId, ITuiWorkspace> cache = [];
    private readonly Stack<WorkspaceId> history = [];
    private readonly CancellationTokenSource lifetime = new();
    private readonly HashSet<WorkspaceId> stale = [];
    private readonly Task? changes;
    private ITuiWorkspace? current;
    private int refreshingStale;
    private bool disposed;

    public TuiShell(
        NavigationProjection navigation,
        IReadOnlyDictionary<WorkspaceId, Func<ITuiWorkspace>> factories,
        IStoreChangeSource? monitor = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(factories);
        this.factories = factories;
        visible = navigation.Items.Select(static item => item.Id).ToHashSet();
        Routes = TuiRoutes.All
            .Where(route => visible.Contains(route.Id) && factories.ContainsKey(route.Id))
            .ToArray();
        Status = navigation.Errors.Count == 0 ? null : TuiErrorAdapter.Adapt(navigation.Errors[0]);
        changes = monitor is null ? null : ObserveChangesAsync(monitor, lifetime.Token);
    }

    public IReadOnlyList<TuiRoute> Routes { get; }
    public TuiRoute CurrentRoute => current is null
        ? TuiRoutes.Dashboard
        : TuiRoutes.All.Single(route => route.Id == current.Id);
    public InputOwnership InputOwnership => current?.InputOwnership ?? InputOwnership.Browse;
    public bool ShowHelp { get; private set; }
    public bool StopRequested { get; private set; }
    public TuiError? Status { get; private set; }
    public event Action? Changed;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!Routes.Any(route => route.Id == TuiRoutes.Dashboard.Id))
        {
            throw AppError.Internal("TUI dashboard route is not registered");
        }

        await MountAsync(TuiRoutes.Dashboard.Id, pushHistory: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> NavigateAsync(WorkspaceId target, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (current?.Id == target || !Routes.Any(route => route.Id == target))
        {
            return false;
        }

        await MountAsync(target, pushHistory: current is not null, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> BackAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (ShowHelp)
        {
            ShowHelp = false;
            Changed?.Invoke();
            return true;
        }

        if (current?.InputOwnership.HandlesBack == true)
        {
            _ = current.Handle('\u001b');
            Changed?.Invoke();
            return true;
        }

        WorkspaceId target = history.Count == 0 ? TuiRoutes.Dashboard.Id : history.Pop();
        if (current?.Id == target)
        {
            return false;
        }

        await MountAsync(target, pushHistory: false, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> HandleAsync(char key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (current is not null && current.InputOwnership.CapturesText)
        {
            _ = current.Handle(key);
            Changed?.Invoke();
            return true;
        }

        if (current?.Handle(key) == true)
        {
            Changed?.Invoke();
            return true;
        }

        switch (key)
        {
            case 'q':
            case 'Q':
                StopRequested = true;
                lifetime.Cancel();
                Changed?.Invoke();
                return true;
            case '?':
                ShowHelp = !ShowHelp;
                Changed?.Invoke();
                return true;
            case 'r':
            case 'R':
                if (current is null)
                {
                    return false;
                }

                try
                {
                    using CancellationTokenSource linked = Link(cancellationToken);
                    await current.RefreshAsync(linked.Token).ConfigureAwait(false);
                }
                catch (Exception exception) when (AppError.IsCancellation(exception))
                {
                }

                return true;
            default:
                TuiRoute? route = Routes.SingleOrDefault(candidate => candidate.Shortcut == key);
                if (route is not null)
                {
                    return await NavigateAsync(route.Id, cancellationToken).ConfigureAwait(false);
                }

                return false;
        }
    }

    public string Render(Viewport viewport)
    {
        ThrowIfDisposed();
        if (viewport.Width < MinimumWidth || viewport.Height < MinimumHeight)
        {
            return $"Terminal too small\nMinimum: {MinimumWidth}x{MinimumHeight}\nCurrent: {viewport.Width}x{viewport.Height}";
        }

        int helpHeight = ShowHelp ? 2 : 0;
        string body = current?.Render(new Viewport(
            viewport.Width,
            Math.Max(viewport.Height - 3 - helpHeight, 0)))
            ?? "Loading Mixology...";
        string status = current?.Status?.Message ?? Status?.Message ??
            $"View: {CurrentRoute.Label} · Press ? for help";
        string help = ShowHelp
            ? $"\n{string.Join("  ", Routes.Where(static route => route.Shortcut is not null).Select(static route => route.Hint))}\n[r] refresh  [Esc] back  [q] quit"
            : string.Empty;
        return Fit($"Mixology > {CurrentRoute.Label}\n{body}\n{status}{help}", viewport);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        lifetime.Cancel();
        if (changes is not null)
        {
            try
            {
                await changes.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        foreach (ITuiWorkspace workspace in cache.Values)
        {
            workspace.Changed -= OnWorkspaceChanged;
            await workspace.DisposeAsync().ConfigureAwait(false);
        }

        cache.Clear();
        lifetime.Dispose();
    }

    public void Report(Exception exception)
    {
        ThrowIfDisposed();
        Status = TuiErrorAdapter.Adapt(exception);
        Changed?.Invoke();
    }

    private async Task MountAsync(
        WorkspaceId target,
        bool pushHistory,
        CancellationToken cancellationToken)
    {
        if (pushHistory && current is not null)
        {
            history.Push(current.Id);
        }

        ITuiWorkspace? prior = null;
        if (target == TuiRoutes.Dashboard.Id)
        {
            lock (sync)
            {
                _ = cache.Remove(target, out prior);
            }
        }

        if (prior is not null)
        {
            prior.Changed -= OnWorkspaceChanged;
            await prior.DisposeAsync().ConfigureAwait(false);
        }

        ITuiWorkspace? workspace;
        lock (sync)
        {
            _ = cache.TryGetValue(target, out workspace);
        }

        if (workspace is null)
        {
            workspace = factories[target]();
            if (workspace.Id != target)
            {
                await workspace.DisposeAsync().ConfigureAwait(false);
                throw AppError.Internal($"TUI workspace factory returned {workspace.Id} for {target}");
            }

            lock (sync)
            {
                cache.Add(target, workspace);
            }
            workspace.Changed += OnWorkspaceChanged;
            current = workspace;
            ShowHelp = false;
            Changed?.Invoke();
            using CancellationTokenSource linked = Link(cancellationToken);
            await workspace.ActivateAsync(linked.Token).ConfigureAwait(false);
            return;
        }

        current = workspace;
        ShowHelp = false;
        Changed?.Invoke();
        if (TakeStale(target))
        {
            using CancellationTokenSource linked = Link(cancellationToken);
            await workspace.ActivateAsync(linked.Token).ConfigureAwait(false);
        }
    }

    private CancellationTokenSource Link(CancellationToken cancellationToken) =>
        CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);

    private void OnWorkspaceChanged()
    {
        Changed?.Invoke();
        if (current is { InputOwnership: var ownership } workspace &&
            !ownership.CapturesText &&
            !ownership.HandlesBack &&
            IsStale(workspace.Id))
        {
            _ = RefreshStaleAsync(workspace, lifetime.Token);
        }
    }

    private async Task ObserveChangesAsync(IStoreChangeSource monitor, CancellationToken cancellationToken)
    {
        await foreach (long epoch in monitor.Changes.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            _ = epoch;
            lock (sync)
            {
                foreach (WorkspaceId id in cache.Keys)
                {
                    _ = stale.Add(id);
                }
            }

            if (current is { InputOwnership: var ownership } workspace &&
                !ownership.CapturesText &&
                !ownership.HandlesBack)
            {
                await RefreshStaleAsync(workspace, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RefreshStaleAsync(ITuiWorkspace workspace, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref refreshingStale, 1) != 0)
        {
            return;
        }

        try
        {
            while (current == workspace && TakeStale(workspace.Id))
            {
                await workspace.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
        }
        catch (Exception exception)
        {
            Status = TuiErrorAdapter.Adapt(exception);
            Changed?.Invoke();
        }
        finally
        {
            _ = Interlocked.Exchange(ref refreshingStale, 0);
        }
    }

    private bool IsStale(WorkspaceId id)
    {
        lock (sync)
        {
            return stale.Contains(id);
        }
    }

    private bool TakeStale(WorkspaceId id)
    {
        lock (sync)
        {
            return stale.Remove(id);
        }
    }

    private static string Fit(string value, Viewport viewport)
    {
        IEnumerable<string> lines = value
            .Split('\n')
            .Select(line => line.Length <= viewport.Width ? line : line[..viewport.Width]);
        return string.Join('\n', lines.Take(viewport.Height));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
