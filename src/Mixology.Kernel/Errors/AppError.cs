namespace Mixology.Kernel.Errors;

public abstract class AppError : Exception
{
    private protected AppError(ErrorKind kind)
        : this(kind, null, null, null)
    {
    }

    private protected AppError(ErrorKind kind, string? detail)
        : this(kind, detail, null, null)
    {
    }

    private protected AppError(ErrorKind kind, string? detail, Exception? innerException)
        : this(kind, detail, null, innerException)
    {
    }

    private protected AppError(ErrorKind kind, string? detail, string? userMessage, Exception? innerException)
        : base(string.IsNullOrEmpty(detail) ? ErrorCatalog.For(kind).DefaultMessage : detail, innerException)
    {
        Kind = kind;
        UserMessageOverride = string.IsNullOrEmpty(userMessage) ? null : userMessage;
    }

    public ErrorKind Kind { get; }

    public ErrorSpec Spec => ErrorCatalog.For(Kind);

    public int HttpStatus => Spec.HttpStatus;

    public int GrpcStatus => Spec.GrpcStatus;

    public int CliExitCode => Spec.CliExitCode;

    public TerminalErrorStyle TerminalStyle => Spec.TerminalStyle;

    public string UserMessage => UserMessageOverride ?? (Kind == ErrorKind.Internal ? Spec.DefaultMessage : Message);

    protected string? UserMessageOverride { get; }

    public abstract AppError WithUserMessage(string message);

    public static InvalidError Invalid(string? detail = null, Exception? cause = null) => new(detail, cause);

    public static NotFoundError NotFound(string? detail = null, Exception? cause = null) => new(detail, cause);

    public static PermissionError Permission(string? detail = null, Exception? cause = null) => new(detail, cause);

    public static ConflictError Conflict(string? detail = null, Exception? cause = null) => new(detail, cause);

    public static FailedPreconditionError FailedPrecondition(string? detail = null, Exception? cause = null) => new(detail, cause);

    public static InternalError Internal(string? detail = null, Exception? cause = null) => new(detail, cause);

    public static bool Is(Exception? exception, ErrorKind kind)
    {
        foreach (Exception candidate in Traverse(exception))
        {
            if (candidate is AppError error && error.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsInvalid(Exception? exception) => Find<InvalidError>(exception) is not null;

    public static bool IsNotFound(Exception? exception) => Find<NotFoundError>(exception) is not null;

    public static bool IsPermission(Exception? exception) => Find<PermissionError>(exception) is not null;

    public static bool IsConflict(Exception? exception) => Find<ConflictError>(exception) is not null;

    public static bool IsFailedPrecondition(Exception? exception) => Find<FailedPreconditionError>(exception) is not null;

    public static bool IsInternal(Exception? exception) => Find<InternalError>(exception) is not null;

    public static bool IsCancellation(Exception? exception) =>
        Find<OperationCanceledException>(exception) is not null;

    public static TException? Find<TException>(Exception? exception)
        where TException : Exception
    {
        foreach (Exception candidate in Traverse(exception))
        {
            if (candidate is TException match)
            {
                return match;
            }
        }

        return null;
    }

    public static AppError? Find(Exception? exception) => Find<AppError>(exception);

    private static IEnumerable<Exception> Traverse(Exception? exception)
    {
        if (exception is null)
        {
            yield break;
        }

        Stack<Exception> pending = new();
        pending.Push(exception);

        while (pending.TryPop(out Exception? current))
        {
            yield return current;

            if (current is AggregateException aggregate)
            {
                for (int index = aggregate.InnerExceptions.Count - 1; index >= 0; index--)
                {
                    pending.Push(aggregate.InnerExceptions[index]);
                }

                continue;
            }

            if (current.InnerException is not null)
            {
                pending.Push(current.InnerException);
            }
        }
    }
}
