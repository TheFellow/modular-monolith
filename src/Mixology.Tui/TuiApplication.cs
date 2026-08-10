using System.CommandLine;
using Mixology.Kernel.Errors;

namespace Mixology.Tui;

public static class TuiApplication
{
    public static RootCommand Build(
        ITuiRuntime? runtime = null,
        TextWriter? error = null)
    {
        runtime ??= new HostedTuiRuntime();
        error ??= Console.Error;
        RootCommand root = new("Interactive terminal client for Mixology.");
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
            Description = "Write diagnostics to this file.",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("MIXOLOGY_LOG_FILE") ?? string.Empty,
        };
        Option<bool> metrics = new("--metrics")
        {
            Description = "Expose Prometheus metrics on localhost:9090/metrics while the TUI runs.",
            DefaultValueFactory = _ => EnvironmentBoolean("MIXOLOGY_METRICS"),
        };
        root.Options.Add(database);
        root.Options.Add(actor);
        root.Options.Add(logLevel);
        root.Options.Add(logFormat);
        root.Options.Add(logFile);
        root.Options.Add(metrics);
        root.SetAction(async (result, cancellationToken) =>
        {
            try
            {
                TuiOptions options = TuiOptions.Create(
                    result.GetValue(database),
                    result.GetValue(actor),
                    result.GetValue(logLevel),
                    result.GetValue(logFormat),
                    result.GetValue(logFile),
                    result.GetValue(metrics));
                await runtime.RunAsync(options, cancellationToken).ConfigureAwait(false);
                return ErrorCatalog.ExitSuccess;
            }
            catch (Exception exception)
            {
                return await TuiErrorAdapter.WriteAsync(error, exception).ConfigureAwait(false);
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
