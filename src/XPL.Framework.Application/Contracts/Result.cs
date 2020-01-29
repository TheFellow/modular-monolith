using System;
using System.Threading.Tasks;
using XPL.Modules.Kernel;

namespace XPL.Framework.Application.Contracts
{
    public abstract class Result<T> : ICorrelate
    {
        public Guid CorrelationId { get; }
        public Result(Guid correlationId) => CorrelationId = correlationId;

        public static implicit operator Task<Result<T>>(Result<T> result) => Task.FromResult(result);

        #region OnOk and OnFail overloads
        
        public Result<T> OnOk(Action action)
        {
            if (this is Ok<T>)
                action();

            return this;
        }

        public async Task<Result<T>> OnOk(Func<Task> action)
        {
            if (this is Ok<T>)
                await action();

            return this;
        }

        public Result<T> OnOk(Action<T> action)
        {
            if (this is Ok<T> ok)
                action(ok.Value);

            return this;
        }

        public async Task<Result<T>> OnOk(Func<T, Task> action)
        {
            if (this is Ok<T> ok)
                await action(ok.Value);

            return this;
        }

        public Result<T> OnFail(Action action)
        {
            if (this is Error<T>)
                action();

            return this;
        }

        public async Task<Result<T>> OnFail(Func<Task> action)
        {
            if (this is Error<T>)
                await action();

            return this;
        }

        public Result<T> OnFail(Action<Error<T>> action)
        {
            if (this is Error<T> error)
                action(error);

            return this;
        }

        public async Task<Result<T>> OnFail(Func<Error<T>, Task> action)
        {
            if (this is Error<T> error)
                await action(error);

            return this;
        }

        #endregion
    }

    public sealed class Ok<T> : Result<T>
    {
        public T Value { get; }
        public Ok(Guid correlationId, T value) : base(correlationId) => Value = value;
    }

    public sealed class Error<T> : Result<T>
    {
        public string Message { get; }
        public DomainException? InnerException { get; }

        public Error(Guid correlationId, string error) : base(correlationId) => Message = error;
        public Error(DomainException ex) : this(ex.CorrelationId, ex.Message) => InnerException = ex;
        public override string ToString() => Message;
    }
}
