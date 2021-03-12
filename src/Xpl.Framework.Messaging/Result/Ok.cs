namespace Xpl.Framework.Messaging.Result
{
    public class Ok<T> : Result<T>
    {
        public T Value { get; init; }
        public Ok(T value) => Value = value;
    }
}
