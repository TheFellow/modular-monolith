namespace Xpl.Framework.Messaging.Commands
{
    public abstract class Result<T>
    {
        public static implicit operator Result<T>(T value) => new Ok<T>(value);

        public static Result<T> Ok(T value) => new Ok<T>(value);
        public static Result<T> Error(string message) => new Error<T>(message);
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
}
