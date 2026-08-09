namespace Mixology.Kernel.Errors;

public enum ErrorKind
{
    Invalid,
    NotFound,
    Permission,
    Conflict,
    FailedPrecondition,
    Internal,
}

public enum TerminalErrorStyle
{
    Error,
    Warning,
    Information,
}

public sealed record ErrorSpec(
    ErrorKind Kind,
    string Name,
    string DefaultMessage,
    int HttpStatus,
    int GrpcStatus,
    int CliExitCode,
    TerminalErrorStyle TerminalStyle);

