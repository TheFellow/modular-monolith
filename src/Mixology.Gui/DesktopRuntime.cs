using Mixology.Gui.Navigation;
using Mixology.Gui.Threading;
using Mixology.Kernel.Errors;

namespace Mixology.Gui;

public sealed class DesktopSession : IAsyncDisposable
{
    private readonly DesktopHost host;
    private bool disposed;

    private DesktopSession(DesktopHost host, ShellViewModel shell)
    {
        this.host = host;
        Shell = shell;
    }

    public ShellViewModel Shell { get; }

    public static DesktopSession Open(DesktopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        DesktopHost? host = null;
        try
        {
            host = DesktopHost.OpenAsync(options).GetAwaiter().GetResult();
            ShellViewModel shell = DesktopShellFactory.CreateAsync(
                host.Services,
                options.Actor,
                new MauiDirtyNavigationConfirmation(),
                new MauiUiDispatcher()).GetAwaiter().GetResult();
            return new DesktopSession(host, shell);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception) || AppError.Find(exception) is not null)
        {
            host?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }
        catch (Exception exception)
        {
            host?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw AppError.Internal("open desktop session", exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            await Shell.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }
    }
}
