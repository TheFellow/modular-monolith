namespace Mixology.Kernel.Errors;

public sealed class AppError : Exception
{
    public AppError(ErrorKind kind)
        : this(kind, null, null, null)
    {
    }

    public AppError(ErrorKind kind, string? detail)
        : this(kind, detail, null, null)
    {
    }

    public AppError(ErrorKind kind, string? detail, Exception? innerException)
        : this(kind, detail, null, innerException)
    {
    }

    private AppError(ErrorKind kind, string? detail, string? userMessage, Exception? innerException)
        : base(string.IsNullOrWhiteSpace(detail) ? ErrorCatalog.For(kind).DefaultMessage : detail, innerException)
    {
        Kind = kind;
        UserMessageOverride = userMessage;
    }

    public ErrorKind Kind { get; }

    public ErrorSpec Spec => ErrorCatalog.For(Kind);

    public string UserMessage => UserMessageOverride ?? (Kind == ErrorKind.Internal ? Spec.DefaultMessage : Message);

    private string? UserMessageOverride { get; }

    public AppError WithUserMessage(string message) =>
        new(Kind, Message, message, InnerException);

    public static AppError Invalid(string detail, Exception? cause = null) =>
        new(ErrorKind.Invalid, detail, cause);

    public static AppError NotFound(string detail, Exception? cause = null) =>
        new(ErrorKind.NotFound, detail, cause);

    public static AppError Permission(string detail, Exception? cause = null) =>
        new(ErrorKind.Permission, detail, cause);

    public static AppError Conflict(string detail, Exception? cause = null) =>
        new(ErrorKind.Conflict, detail, cause);

    public static AppError FailedPrecondition(string detail, Exception? cause = null) =>
        new(ErrorKind.FailedPrecondition, detail, cause);

    public static AppError Internal(string detail, Exception? cause = null) =>
        new(ErrorKind.Internal, detail, cause);

    public static bool Is(Exception exception, ErrorKind kind) =>
        Find(exception)?.Kind == kind;

    public static AppError? Find(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is AppError error)
            {
                return error;
            }

            exception = exception.InnerException;
        }

        return null;
    }
}

