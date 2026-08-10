using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace Mixology.Toolkits.Tui;

public enum TerminalApplicationState
{
    Created,
    Initialized,
    Running,
    Disposed,
}

public sealed class TerminalApplicationHost : IDisposable
{
    private readonly IApplication application;
    private readonly string? driverName;

    private TerminalApplicationHost(IApplication application, string? driverName)
    {
        this.application = application;
        this.driverName = driverName;
    }

    public TerminalApplicationState State { get; private set; }

    public IApplication Application
    {
        get
        {
            ThrowIfDisposed();
            return application;
        }
    }

    public static TerminalApplicationHost Create(string? driverName = null) =>
        new(Terminal.Gui.App.Application.Create(), driverName);

    public static TerminalApplicationHost CreateAnsi() => Create(DriverRegistry.Names.ANSI);

    public void Initialize()
    {
        RequireState(TerminalApplicationState.Created, "initialize");
        if (driverName is null)
        {
            _ = application.Init();
        }
        else
        {
            _ = application.Init(driverName);
        }

        State = TerminalApplicationState.Initialized;
    }

    public async Task RunAsync(IRunnable root, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        cancellationToken.ThrowIfCancellationRequested();
        if (State == TerminalApplicationState.Created)
        {
            Initialize();
        }

        RequireState(TerminalApplicationState.Initialized, "run");
        State = TerminalApplicationState.Running;
        try
        {
            _ = await application.RunAsync(root, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            State = TerminalApplicationState.Initialized;
        }
    }

    public void Dispose()
    {
        if (State == TerminalApplicationState.Disposed)
        {
            return;
        }

        if (State == TerminalApplicationState.Running)
        {
            throw new TuiLifecycleException("cannot dispose a running terminal application");
        }

        application.Dispose();
        State = TerminalApplicationState.Disposed;
    }

    private void RequireState(TerminalApplicationState required, string operation)
    {
        ThrowIfDisposed();
        if (State != required)
        {
            throw new TuiLifecycleException(
                $"cannot {operation} terminal application while it is {State.ToString().ToLowerInvariant()}");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            State == TerminalApplicationState.Disposed,
            this);
    }
}

public sealed class TuiLifecycleException(string message) : InvalidOperationException(message);
