namespace Mixology.Kernel.Errors;

public sealed class InvalidError : AppError
{
    public InvalidError(string? detail = null, Exception? cause = null)
        : this(detail, null, cause)
    {
    }

    private InvalidError(string? detail, string? userMessage, Exception? cause)
        : base(ErrorKind.Invalid, detail, userMessage, cause)
    {
    }

    public override InvalidError WithUserMessage(string message) =>
        new(Message, message, InnerException);
}

public sealed class NotFoundError : AppError
{
    public NotFoundError(string? detail = null, Exception? cause = null)
        : this(detail, null, cause)
    {
    }

    private NotFoundError(string? detail, string? userMessage, Exception? cause)
        : base(ErrorKind.NotFound, detail, userMessage, cause)
    {
    }

    public override NotFoundError WithUserMessage(string message) =>
        new(Message, message, InnerException);
}

public sealed class PermissionError : AppError
{
    public PermissionError(string? detail = null, Exception? cause = null)
        : this(detail, null, cause)
    {
    }

    private PermissionError(string? detail, string? userMessage, Exception? cause)
        : base(ErrorKind.Permission, detail, userMessage, cause)
    {
    }

    public override PermissionError WithUserMessage(string message) =>
        new(Message, message, InnerException);
}

public sealed class ConflictError : AppError
{
    public ConflictError(string? detail = null, Exception? cause = null)
        : this(detail, null, cause)
    {
    }

    private ConflictError(string? detail, string? userMessage, Exception? cause)
        : base(ErrorKind.Conflict, detail, userMessage, cause)
    {
    }

    public override ConflictError WithUserMessage(string message) =>
        new(Message, message, InnerException);
}

public sealed class FailedPreconditionError : AppError
{
    public FailedPreconditionError(string? detail = null, Exception? cause = null)
        : this(detail, null, cause)
    {
    }

    private FailedPreconditionError(string? detail, string? userMessage, Exception? cause)
        : base(ErrorKind.FailedPrecondition, detail, userMessage, cause)
    {
    }

    public override FailedPreconditionError WithUserMessage(string message) =>
        new(Message, message, InnerException);
}

public sealed class InternalError : AppError
{
    public InternalError(string? detail = null, Exception? cause = null)
        : this(detail, null, cause)
    {
    }

    private InternalError(string? detail, string? userMessage, Exception? cause)
        : base(ErrorKind.Internal, detail, userMessage, cause)
    {
    }

    public override InternalError WithUserMessage(string message) =>
        new(Message, message, InnerException);
}
