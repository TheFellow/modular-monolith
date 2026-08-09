namespace Mixology.Kernel.Errors;

public abstract class AppError : Exception
{
    protected AppError(ErrorKind kind)
        : this(kind, null, null, null)
    {
    }

    protected AppError(ErrorKind kind, string? detail)
        : this(kind, detail, null, null)
    {
    }

    protected AppError(ErrorKind kind, string? detail, Exception? innerException)
        : this(kind, detail, null, innerException)
    {
    }

    protected AppError(ErrorKind kind, string? detail, string? userMessage, Exception? innerException)
        : base(string.IsNullOrWhiteSpace(detail) ? ErrorCatalog.For(kind).DefaultMessage : detail, innerException)
    {
        Kind = kind;
        UserMessageOverride = userMessage;
    }

    public ErrorKind Kind { get; }

    public ErrorSpec Spec => ErrorCatalog.For(Kind);

    public string UserMessage => UserMessageOverride ?? (Kind == ErrorKind.Internal ? Spec.DefaultMessage : Message);

    protected string? UserMessageOverride { get; }

    public abstract AppError WithUserMessage(string message);

    public static InvalidError Invalid(string detail, Exception? cause = null) => new(detail, cause);

    public static NotFoundError NotFound(string detail, Exception? cause = null) => new(detail, cause);

    public static PermissionError Permission(string detail, Exception? cause = null) => new(detail, cause);

    public static ConflictError Conflict(string detail, Exception? cause = null) => new(detail, cause);

    public static FailedPreconditionError FailedPrecondition(string detail, Exception? cause = null) => new(detail, cause);

    public static InternalError Internal(string detail, Exception? cause = null) => new(detail, cause);

    public static bool Is(Exception exception, ErrorKind kind) =>
        Find(exception)?.Kind == kind;

    public static bool IsInvalid(Exception exception) => Find<InvalidError>(exception) is not null;

    public static bool IsNotFound(Exception exception) => Find<NotFoundError>(exception) is not null;

    public static bool IsPermission(Exception exception) => Find<PermissionError>(exception) is not null;

    public static bool IsConflict(Exception exception) => Find<ConflictError>(exception) is not null;

    public static bool IsFailedPrecondition(Exception exception) => Find<FailedPreconditionError>(exception) is not null;

    public static bool IsInternal(Exception exception) => Find<InternalError>(exception) is not null;

    public static TError? Find<TError>(Exception? exception)
        where TError : AppError
    {
        if (exception is null)
        {
            return null;
        }

        if (exception is TError match)
        {
            return match;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (Exception inner in aggregate.InnerExceptions)
            {
                TError? nested = Find<TError>(inner);
                if (nested is not null)
                {
                    return nested;
                }
            }

            return null;
        }

        return Find<TError>(exception.InnerException);
    }

    public static AppError? Find(Exception? exception)
    {
        if (exception is null)
        {
            return null;
        }

        if (exception is AppError error)
        {
            return error;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (Exception inner in aggregate.InnerExceptions)
            {
                AppError? nested = Find(inner);
                if (nested is not null)
                {
                    return nested;
                }
            }

            return null;
        }

        return Find(exception.InnerException);
    }
}
