using System;

namespace Xpl.Framework.Messaging.Result
{
    public class Error<T> : Result<T>
    {
        public string Message { get; }
        public Exception? Exception { get; }
        public Error(Exception? ex, string message)
        {
            Exception = ex;
            Message = message;
        }
        public Error(string message) : this(null, message) { }
        public Error(Exception ex) : this(ex, ex.Message) { }
    }
}
