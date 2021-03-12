namespace Xpl.Framework.Messaging.MediatR
{
    public class CommandResult<T>
    {
        public T Result { get; }

        public CommandResult(T result) => Result = result;
    }
}
