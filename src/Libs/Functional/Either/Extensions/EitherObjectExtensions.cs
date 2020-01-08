using System;

namespace Functional.Either
{
    public static class EitherObjectExtensions
    {
        /// <summary>
        /// Converts <typeparamref name="TRight"/> to an <see cref="Either{TLeft, TRight}"/> by returning
        /// <see cref="Right{TLeft, TRight}"/> when <paramref name="predicate"/> is true and <see cref="Left{TLeft, TRight}"/>
        /// otherwise
        /// </summary>
        public static Either<TLeft, TRight> When<TLeft, TRight>(this TRight obj, bool predicate, TLeft whenFalse) =>
            predicate
                ? (Either<TLeft, TRight>)obj
                : whenFalse;

        /// <summary>
        /// Converts <typeparamref name="TRight"/> to an <see cref="Either{TLeft, TRight}"/> by returning
        /// <see cref="Right{TLeft, TRight}"/> when <paramref name="predicate"/> is true and <see cref="Left{TLeft, TRight}"/>
        /// otherwise
        /// </summary>
        public static Either<TLeft, TRight> When<TLeft, TRight>(this TRight obj, bool predicate, Func<TLeft> whenFalse) =>
            predicate
                ? (Either<TLeft, TRight>)obj
                : whenFalse();

        /// <summary>
        /// Converts <typeparamref name="TRight"/> to an <see cref="Either{TLeft, TRight}"/> by returning
        /// <see cref="Right{TLeft, TRight}"/> when <paramref name="predicate"/> is true and <see cref="Left{TLeft, TRight}"/>
        /// otherwise
        /// </summary>
        public static Either<TLeft, TRight> When<TLeft, TRight>(this TRight obj, bool predicate, Func<TRight, TLeft> whenFalse) =>
            predicate
                ? (Either<TLeft, TRight>)obj
                : whenFalse(obj);

        /// <summary>
        /// Converts <typeparamref name="TRight"/> to an <see cref="Either{TLeft, TRight}"/> by returning
        /// <see cref="Right{TLeft, TRight}"/> when <paramref name="predicate"/> is true and <see cref="Left{TLeft, TRight}"/>
        /// otherwise
        /// </summary>
        public static Either<TLeft, TRight> When<TLeft, TRight>(this TRight obj, Func<bool> predicate, TLeft whenFalse) =>
            predicate()
                ? (Either<TLeft, TRight>)obj
                : whenFalse;

        /// <summary>
        /// Converts <typeparamref name="TRight"/> to an <see cref="Either{TLeft, TRight}"/> by returning
        /// <see cref="Right{TLeft, TRight}"/> when <paramref name="predicate"/> is true and <see cref="Left{TLeft, TRight}"/>
        /// otherwise
        /// </summary>
        public static Either<TLeft, TRight> When<TLeft, TRight>(this TRight obj, Func<bool> predicate, Func<TLeft> whenFalse) =>
            predicate()
                ? (Either<TLeft, TRight>)obj
                : whenFalse();

        /// <summary>
        /// Converts <typeparamref name="TRight"/> to an <see cref="Either{TLeft, TRight}"/> by returning
        /// <see cref="Right{TLeft, TRight}"/> when <paramref name="predicate"/> is true and <see cref="Left{TLeft, TRight}"/>
        /// otherwise
        /// </summary>
        public static Either<TLeft, TRight> When<TLeft, TRight>(this TRight obj, Func<bool> predicate, Func<TRight, TLeft> whenFalse) =>
            predicate()
                ? (Either<TLeft, TRight>)obj
                : whenFalse(obj);

        /// <summary>
        /// Converts <typeparamref name="TRight"/> to an <see cref="Either{TLeft, TRight}"/> by returning
        /// <see cref="Right{TLeft, TRight}"/> when <paramref name="predicate"/> is true and <see cref="Left{TLeft, TRight}"/>
        /// otherwise
        /// </summary>
        public static Either<TLeft, TRight> When<TLeft, TRight>(this TRight obj, Func<TRight, bool> predicate, TLeft whenFalse) =>
            predicate(obj)
                ? (Either<TLeft, TRight>)obj
                : whenFalse;

        /// <summary>
        /// Converts <typeparamref name="TRight"/> to an <see cref="Either{TLeft, TRight}"/> by returning
        /// <see cref="Right{TLeft, TRight}"/> when <paramref name="predicate"/> is true and <see cref="Left{TLeft, TRight}"/>
        /// otherwise
        /// </summary>
        public static Either<TLeft, TRight> When<TLeft, TRight>(this TRight obj, Func<TRight, bool> predicate, Func<TLeft> whenFalse) =>
            predicate(obj)
                ? (Either<TLeft, TRight>)obj
                : whenFalse();

        /// <summary>
        /// Converts <typeparamref name="TRight"/> to an <see cref="Either{TLeft, TRight}"/> by returning
        /// <see cref="Right{TLeft, TRight}"/> when <paramref name="predicate"/> is true and <see cref="Left{TLeft, TRight}"/>
        /// otherwise
        /// </summary>
        public static Either<TLeft, TRight> When<TLeft, TRight>(this TRight obj, Func<TRight, bool> predicate, Func<TRight, TLeft> whenFalse) =>
            predicate(obj)
                ? (Either<TLeft, TRight>)obj
                : whenFalse(obj);
    }
}
