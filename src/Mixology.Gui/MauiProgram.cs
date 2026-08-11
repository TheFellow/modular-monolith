using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Mixology.Kernel.Errors;

namespace Mixology.Gui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        DesktopOptions options = ParseOptions(Environment.GetCommandLineArgs()[1..]);
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<DesktopApplication>();
        builder.Services.AddSingleton(options);
        return builder.Build();
    }

    private static DesktopOptions ParseOptions(string[] args)
    {
        CapturingRuntime runtime = new();
        StringWriter errors = new();
        int exitCode = DesktopCommandLine.Build(runtime, errors).Parse(args).Invoke();
        if (exitCode != ErrorCatalog.ExitSuccess || runtime.Options is null)
        {
            string message = errors.ToString().Trim();
            throw AppError.Invalid(message.Length == 0
                ? "desktop options did not start the application"
                : message);
        }

        return runtime.Options;
    }

    private sealed class CapturingRuntime : IDesktopRuntime
    {
        public DesktopOptions? Options { get; private set; }

        public int Run(DesktopOptions options)
        {
            Options = options;
            return ErrorCatalog.ExitSuccess;
        }
    }
}
