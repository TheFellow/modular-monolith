using System.CommandLine;
using Mixology.Kernel.Errors;

namespace Mixology.Desktop;

public static class DesktopCommandLine
{
    public static RootCommand Build(IDesktopRuntime? runtime = null, TextWriter? error = null)
    {
        runtime ??= new HostedDesktopRuntime();
        error ??= Console.Error;
        RootCommand root = new("Desktop client for Mixology.");
        Option<string> database = new("--db")
        {
            Description = "Path to the device-local Mixology SQLite database.",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("MIXOLOGY_DB")
                ?? Path.GetFullPath(Path.Combine("data", "mixology.db")),
        };
        Option<string> actor = new("--actor", "--as")
        {
            Description = "Actor identity: owner, manager, sommelier, bartender, or anonymous.",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("MIXOLOGY_ACTOR") ?? "owner",
        };
        root.Options.Add(database);
        root.Options.Add(actor);
        root.SetAction(result =>
        {
            try
            {
                return runtime.Run(DesktopOptions.Create(
                    result.GetValue(database),
                    result.GetValue(actor)));
            }
            catch (Exception exception)
            {
                DesktopError adapted = DesktopErrorAdapter.Adapt(exception);
                error.WriteLine(adapted.Message);
                return adapted.ExitCode;
            }
        });
        return root;
    }
}

public sealed record DesktopError(string Message, int ExitCode);

public static class DesktopErrorAdapter
{
    public static DesktopError Adapt(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        AppError? applicationError = AppError.Find(exception);
        if (applicationError is not null)
        {
            return new DesktopError(applicationError.UserMessage, applicationError.CliExitCode);
        }

        return AppError.IsCancellation(exception)
            ? new DesktopError("operation cancelled", ErrorCatalog.ExitGeneral)
            : new DesktopError("internal error", ErrorCatalog.ExitGeneral);
    }
}
