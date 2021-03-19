using System;
using System.Threading.Tasks;

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

        public static async Task<Result<T>> Handle<T>(this Result<T> result, Func<T, Task> onOk, Func<string, Task> onError)
        {
            var task = result switch
            {
                Ok<T> ok => onOk(ok.Value),
                Error<T> error => onError(error.Message),
                _ => throw new InvalidOperationException("We need discriminated unions")
            };
            await task;
            return result;
        }
    }
}
