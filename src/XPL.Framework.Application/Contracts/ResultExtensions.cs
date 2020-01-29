using XPL.Modules.Kernel;

namespace XPL.Framework.Application.Contracts
{
    public static class ResultExtensions
    {
        public static Result<TResult> Ok<TResult>(this ICommand<TResult> command, TResult value) =>
            new Ok<TResult>(command.CorrelationId, value);

        public static Result<TResult> Fail<TResult>(this ICommand<TResult> command, string error) =>
            new Error<TResult>(command.CorrelationId, error);

        public static Result<TResult> Fail<TResult>(this ICommand<TResult> _, DomainException ex) =>
            new Error<TResult>(ex);
    }
}
