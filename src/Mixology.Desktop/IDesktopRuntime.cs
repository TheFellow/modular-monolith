using Mixology.Kernel.Errors;

namespace Mixology.Desktop;

public interface IDesktopRuntime
{
    int Run(DesktopOptions options);
}

public sealed class HostedDesktopRuntime : IDesktopRuntime
{
    public int Run(DesktopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        throw AppError.Internal(
            "start native desktop lifetime",
            new NotSupportedException("The native lifetime is started by the .NET MAUI platform host."));
    }
}
