using Mixology.Kernel.Errors;

namespace Mixology.Tui;

public sealed record TuiError(string Message, TerminalErrorStyle Style);

public static class TuiErrorAdapter
{
    public static TuiError Adapt(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        AppError? applicationError = AppError.Find(exception);
        if (applicationError is not null)
        {
            return new TuiError(applicationError.UserMessage, applicationError.TerminalStyle);
        }

        return AppError.IsCancellation(exception)
            ? new TuiError("operation cancelled", TerminalErrorStyle.Information)
            : new TuiError("internal error", TerminalErrorStyle.Error);
    }

    public static async Task<int> WriteAsync(TextWriter error, Exception exception)
    {
        TuiError adapted = Adapt(exception);
        await error.WriteLineAsync(adapted.Message).ConfigureAwait(false);
        return AppError.Find(exception)?.CliExitCode ?? ErrorCatalog.ExitGeneral;
    }
}
