namespace Mixology.Kernel.Errors;

public static class ErrorCatalog
{
    public const int ExitSuccess = 0;
    public const int ExitGeneral = 1;
    public const int ExitUsage = 2;
    public const int ExitInvalid = 10;
    public const int ExitNotFound = 20;
    public const int ExitPermission = 30;
    public const int ExitConflict = 40;
    public const int ExitFailedPrecondition = 45;
    public const int ExitInternal = 50;

    private static readonly Dictionary<ErrorKind, ErrorSpec> Specs =
        new Dictionary<ErrorKind, ErrorSpec>
        {
            [ErrorKind.Invalid] = new(ErrorKind.Invalid, "Invalid", "invalid", 400, 3, ExitInvalid, TerminalErrorStyle.Error),
            [ErrorKind.NotFound] = new(ErrorKind.NotFound, "NotFound", "not found", 404, 5, ExitNotFound, TerminalErrorStyle.Warning),
            [ErrorKind.Permission] = new(ErrorKind.Permission, "Permission", "permission denied", 403, 7, ExitPermission, TerminalErrorStyle.Error),
            [ErrorKind.Conflict] = new(ErrorKind.Conflict, "Conflict", "conflict", 409, 6, ExitConflict, TerminalErrorStyle.Warning),
            [ErrorKind.FailedPrecondition] = new(ErrorKind.FailedPrecondition, "FailedPrecondition", "failed precondition", 412, 9, ExitFailedPrecondition, TerminalErrorStyle.Warning),
            [ErrorKind.Internal] = new(ErrorKind.Internal, "Internal", "internal error", 500, 13, ExitInternal, TerminalErrorStyle.Error),
        };

    public static IReadOnlyList<ErrorKind> AllKinds { get; } =
        Array.AsReadOnly(Enum.GetValues<ErrorKind>());

    public static ErrorSpec For(ErrorKind kind) =>
        Specs.GetValueOrDefault(kind, Specs[ErrorKind.Internal]);
}
