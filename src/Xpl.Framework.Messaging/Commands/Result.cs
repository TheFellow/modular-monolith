using System.Threading.Tasks;

namespace Xpl.Framework.Messaging.Commands
{
    public abstract class Result<T>
    {
        public static implicit operator Result<T>(T value) => new Ok<T>(value);
        public static implicit operator Task<Result<T>>(Result<T> result) => Task.FromResult(result);
    }

    public class Ok<T> : Result<T>
    {
        public T Value { get; }
        public Ok(T value) => Value = value;
    }

    public class Error<T> : Result<T>
    {
        public string Message { get; }
        public Error(string message) => Message = message;
    }

    public static class Result
    {
        public static Result<T> Ok<T>(T value) => new Ok<T>(value);
        public static Result<T> Error<T>(string message) => new Error<T>(message);

        public static Result<T> Ok<T>(this ICommand<T> command, T value) => new Ok<T>(value);
        public static Result<T> Error<T>(this ICommand<T> _, string message) => new Error<T>(message);
    }
}
