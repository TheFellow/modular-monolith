using System;

namespace Xpl.Framework.Messaging.Commands
{
    public static class ResultExtensions
    {
        public static Result<T> OnOk<T>(this Result<T> result, Action<T> action)
        {
            if (result is Ok<T> ok)
                action(ok.Value);
            return result;
        }

        public static Result<T> OnError<T>(this Result<T> result, Action<string> action)
        {
            if (result is Error<T> error)
                action(error.Message);
            return result;
        }
    }
}
