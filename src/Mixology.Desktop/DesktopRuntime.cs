using Avalonia;
using Mixology.Desktop.Navigation;
using Mixology.Desktop.Threading;
using Mixology.Kernel.Errors;

namespace Mixology.Desktop;

public interface IDesktopRuntime
{
    int Run(DesktopOptions options);
}

public interface IDesktopLifetime
{
    int Run(DesktopApplication application);
}

public sealed class ClassicDesktopLifetime : IDesktopLifetime
{
    public int Run(DesktopApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return AppBuilder.Configure(() => application)
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime([]);
    }
}

public sealed class HostedDesktopRuntime(IDesktopLifetime? lifetime = null) : IDesktopRuntime
{
    private readonly IDesktopLifetime lifetime = lifetime ?? new ClassicDesktopLifetime();

    public int Run(DesktopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        DesktopHost? host = null;
        ShellViewModel? shell = null;
        try
        {
            // Keep Avalonia startup on the process STA thread. Host startup completes before the
            // native lifetime begins, so this synchronous edge is intentional.
            host = DesktopHost.OpenAsync(options).GetAwaiter().GetResult();
            shell = DesktopShellFactory.CreateAsync(
                host.Services,
                options.Actor,
                new AvaloniaDirtyNavigationConfirmation(),
                new AvaloniaUiDispatcher()).GetAwaiter().GetResult();
            return lifetime.Run(new DesktopApplication(shell));
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            throw;
        }
        catch (Exception exception) when (AppError.Find(exception) is not null)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw AppError.Internal("run desktop application", exception);
        }
        finally
        {
            try
            {
                shell?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            finally
            {
                host?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }
}
