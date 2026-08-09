using System.Globalization;
using Mixology.Application.Authentication;
using Mixology.Kernel.Errors;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace Mixology.Desktop;

public sealed record DesktopOptions(
    string DatabasePath,
    Actor Actor,
    LogEventLevel LogLevel,
    string LogFormat,
    string LogFile,
    bool Metrics)
{
    private const string TextTemplate =
        "{Timestamp:yyyy-MM-ddTHH:mm:ss.fffzzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    public DesktopOptions(string databasePath, Actor actor)
        : this(databasePath, actor, LogEventLevel.Information, "text", string.Empty, false)
    {
    }

    public static DesktopOptions Create(string? databasePath, string? actor)
        => Create(databasePath, actor, "info", "text", string.Empty, metrics: false);

    public static DesktopOptions Create(
        string? databasePath,
        string? actor,
        string? logLevel,
        string? logFormat,
        string? logFile,
        bool metrics)
    {
        string database = Path.GetFullPath(
            string.IsNullOrWhiteSpace(databasePath)
                ? Path.Combine("data", "mixology.db")
                : databasePath.Trim());
        string normalizedLevel = logLevel?.Trim().ToLowerInvariant() ?? string.Empty;
        LogEventLevel parsedLevel = normalizedLevel switch
        {
            "debug" => LogEventLevel.Debug,
            "info" => LogEventLevel.Information,
            "warn" or "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            _ => throw AppError.Invalid($"invalid log level \"{logLevel?.Trim()}\""),
        };
        string normalizedFormat = logFormat?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedFormat is not ("text" or "json"))
        {
            throw AppError.Invalid($"invalid log format \"{logFormat?.Trim()}\"");
        }

        return new DesktopOptions(
            database,
            Actor.Parse(actor),
            parsedLevel,
            normalizedFormat,
            logFile?.Trim() ?? string.Empty,
            metrics);
    }

    public void ValidateLogDestination()
    {
        if (LogFile.Length == 0)
        {
            return;
        }

        try
        {
            string path = Path.GetFullPath(LogFile);
            if (Directory.Exists(path))
            {
                throw AppError.Invalid("log file path names a directory");
            }

            using FileStream stream = new(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Invalid("log file cannot be opened", exception);
        }
    }

    public void Configure(LoggerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _ = configuration.MinimumLevel.Is(LogLevel).Enrich.FromLogContext();
        if (string.Equals(LogFormat, "json", StringComparison.Ordinal))
        {
            JsonFormatter formatter = new(renderMessage: true);
            if (LogFile.Length == 0)
            {
                _ = configuration.WriteTo.Console(
                    formatter,
                    standardErrorFromLevel: LogEventLevel.Verbose);
            }
            else
            {
                _ = configuration.WriteTo.File(
                    formatter,
                    Path.GetFullPath(LogFile),
                    shared: true);
            }

            return;
        }

        if (LogFile.Length == 0)
        {
            _ = configuration.WriteTo.Console(
                outputTemplate: TextTemplate,
                formatProvider: CultureInfo.InvariantCulture,
                standardErrorFromLevel: LogEventLevel.Verbose);
        }
        else
        {
            _ = configuration.WriteTo.File(
                Path.GetFullPath(LogFile),
                outputTemplate: TextTemplate,
                formatProvider: CultureInfo.InvariantCulture,
                shared: true);
        }
    }
}
