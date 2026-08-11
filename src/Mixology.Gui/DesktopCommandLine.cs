using System.CommandLine;
using Mixology.Kernel.Errors;

namespace Mixology.Gui;

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
                ?? DesktopOptions.DefaultDatabasePath,
        };
        Option<string> actor = new("--actor", "--as")
        {
            Description = "Actor identity: owner, manager, sommelier, bartender, or anonymous.",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("MIXOLOGY_ACTOR") ?? "owner",
        };
        Option<string> logLevel = new("--log-level")
        {
            Description = "Diagnostic level: debug, info, warn, or error.",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("MIXOLOGY_LOG_LEVEL") ?? "info",
        };
        Option<string> logFormat = new("--log-format")
        {
            Description = "Diagnostic format: text or json.",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("MIXOLOGY_LOG_FORMAT") ?? "text",
        };
        Option<string> logFile = new("--log-file")
        {
            Description = "Write diagnostics to this file instead of stderr.",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("MIXOLOGY_LOG_FILE") ?? string.Empty,
        };
        Option<bool> metrics = new("--metrics")
        {
            Description = "Expose Prometheus metrics on localhost:9090/metrics while the desktop client runs.",
            DefaultValueFactory = _ => EnvironmentBoolean("MIXOLOGY_METRICS"),
        };
        root.Options.Add(database);
        root.Options.Add(actor);
        root.Options.Add(logLevel);
        root.Options.Add(logFormat);
        root.Options.Add(logFile);
        root.Options.Add(metrics);
        root.SetAction(result =>
        {
            try
            {
                return runtime.Run(DesktopOptions.Create(
                    result.GetValue(database),
                    result.GetValue(actor),
                    result.GetValue(logLevel),
                    result.GetValue(logFormat),
                    result.GetValue(logFile),
                    result.GetValue(metrics)));
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

    private static bool EnvironmentBoolean(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "0" or "f" or "false" => false,
            "1" or "t" or "true" => true,
            _ => throw AppError.Invalid($"environment variable {name} must be true or false"),
        };
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
