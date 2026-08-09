using Mixology.Kernel.Errors;
using Xunit;

namespace Mixology.Kernel.Tests.Errors;

public sealed class AppErrorTests
{
    public static TheoryData<ErrorKind, string, string, int, int, int, TerminalErrorStyle> Mappings => new()
    {
        { ErrorKind.Invalid, "Invalid", "invalid", 400, 3, 10, TerminalErrorStyle.Error },
        { ErrorKind.NotFound, "NotFound", "not found", 404, 5, 20, TerminalErrorStyle.Warning },
        { ErrorKind.Permission, "Permission", "permission denied", 403, 7, 30, TerminalErrorStyle.Error },
        { ErrorKind.Conflict, "Conflict", "conflict", 409, 6, 40, TerminalErrorStyle.Warning },
        { ErrorKind.FailedPrecondition, "FailedPrecondition", "failed precondition", 412, 9, 45, TerminalErrorStyle.Warning },
        { ErrorKind.Internal, "Internal", "internal error", 500, 13, 50, TerminalErrorStyle.Error },
    };

    [Theory]
    [MemberData(nameof(Mappings))]
    public void CatalogPreservesTransportMappings(
        ErrorKind kind,
        string name,
        string defaultMessage,
        int httpStatus,
        int grpcStatus,
        int exitCode,
        TerminalErrorStyle style)
    {
        ErrorSpec spec = ErrorCatalog.For(kind);

        Assert.Equal(name, spec.Name);
        Assert.Equal(defaultMessage, spec.DefaultMessage);
        Assert.Equal(httpStatus, spec.HttpStatus);
        Assert.Equal(grpcStatus, spec.GrpcStatus);
        Assert.Equal(exitCode, spec.CliExitCode);
        Assert.Equal(style, spec.TerminalStyle);
    }

    [Fact]
    public void CatalogIsCompleteImmutableAndFallsBackToInternal()
    {
        Assert.Equal(Enum.GetValues<ErrorKind>(), ErrorCatalog.AllKinds);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ErrorKind>)ErrorCatalog.AllKinds)[0] = ErrorKind.Internal);
        Assert.Equal(ErrorCatalog.For(ErrorKind.Internal), ErrorCatalog.For((ErrorKind)byte.MaxValue));
    }

    [Fact]
    public void InternalErrorHidesDiagnosticDetail()
    {
        AppError error = AppError.Internal("database password leaked");

        Assert.Equal("database password leaked", error.Message);
        Assert.Equal("internal error", error.UserMessage);
        Assert.Equal("Please try again", error.WithUserMessage("Please try again").UserMessage);
    }

    [Fact]
    public void NonInternalErrorRetainsActionableDetail()
    {
        AppError error = AppError.Invalid("name is required");

        Assert.Equal("name is required", error.UserMessage);
        Assert.True(AppError.Is(new InvalidOperationException("outer", error), ErrorKind.Invalid));
    }

    [Fact]
    public void EmptyDetailAndSafeOverrideUseKindFallbackWhileWhitespaceRemainsExplicit()
    {
        InvalidError empty = new();
        InvalidError whitespace = new(" ");

        Assert.Equal("invalid", empty.Message);
        Assert.Equal("invalid", empty.WithUserMessage(string.Empty).UserMessage);
        Assert.Equal(" ", whitespace.Message);
        Assert.Equal(" ", whitespace.UserMessage);
    }

    [Fact]
    public void CauseIsPreserved()
    {
        IOException cause = new("disk failed");
        AppError error = AppError.Internal("write failed", cause);

        Assert.Same(cause, error.InnerException);
    }

    [Fact]
    public void EveryKindHasAPreciseRuntimeType()
    {
        Assert.IsType<InvalidError>(AppError.Invalid("invalid"));
        Assert.IsType<NotFoundError>(AppError.NotFound("missing"));
        Assert.IsType<PermissionError>(AppError.Permission("denied"));
        Assert.IsType<ConflictError>(AppError.Conflict("duplicate"));
        Assert.IsType<FailedPreconditionError>(AppError.FailedPrecondition("not ready"));
        Assert.IsType<InternalError>(AppError.Internal("broken"));
        Assert.IsType<InternalError>(AppError.Internal("broken").WithUserMessage("try again"));
    }

    [Fact]
    public void TypedClassificationTraversesWrappedAndJoinedErrors()
    {
        NotFoundError missing = AppError.NotFound("ingredient missing").WithUserMessage("not here");
        AggregateException joined = new(
            new IOException("unrelated"),
            new InvalidOperationException("outer", missing));

        Assert.True(AppError.IsNotFound(joined));
        Assert.False(AppError.IsPermission(joined));
        Assert.Same(missing, AppError.Find<NotFoundError>(joined));
        Assert.Same(missing, AppError.Find(joined));
        Assert.Equal("not here", missing.UserMessage);
    }

    [Fact]
    public void KindMatchingExaminesEveryAggregateBranch()
    {
        InvalidError invalid = AppError.Invalid("invalid");
        PermissionError denied = AppError.Permission("denied");
        AggregateException aggregate = new(
            new InvalidOperationException("first", invalid),
            new IOException("second", denied));

        Assert.True(AppError.Is(aggregate, ErrorKind.Invalid));
        Assert.True(AppError.Is(aggregate, ErrorKind.Permission));
        Assert.False(AppError.Is(aggregate, ErrorKind.Internal));
        Assert.Same(invalid, AppError.Find(aggregate));
        Assert.Same(denied, AppError.Find<PermissionError>(aggregate));
    }

    [Fact]
    public void CancellationRemainsDistinctThroughWrappingAndAggregation()
    {
        TaskCanceledException cancellation = new("cancelled");
        Exception wrapped = new AggregateException(
            new IOException("unrelated"),
            new InvalidOperationException("outer", cancellation));

        Assert.True(AppError.IsCancellation(wrapped));
        Assert.Same(cancellation, AppError.Find<OperationCanceledException>(wrapped));
        Assert.Null(AppError.Find(wrapped));
        Assert.False(AppError.IsInternal(wrapped));
        Assert.False(AppError.IsCancellation(AppError.Internal("failed")));
        Assert.False(AppError.IsCancellation(null));
    }
}
