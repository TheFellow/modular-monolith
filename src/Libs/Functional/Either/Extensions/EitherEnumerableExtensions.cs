using Functional.Option;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Functional.Either
{
    public static class EitherEnumerableExtensions
    {
        /// <summary>
        /// Returns the first element of a sequence of <typeparamref name="TRight"/> else the substitute <typeparamref name="TLeft"/> <paramref name="whenEmpty"/>
        /// </summary>
        public static Either<TLeft, TRight> FirstOrOther<TLeft, TRight>(this IEnumerable<TRight> seq, TLeft whenEmpty) =>
            seq.FirstOrNone() is Some<TRight> some
                ? (Either<TLeft, TRight>)some.Content
                : whenEmpty;

        /// <summary>
        /// Returns the first element of a sequence of <typeparamref name="TRight"/> else the substitute <typeparamref name="TLeft"/> <paramref name="whenEmpty"/>
        /// </summary>
        public static Either<TLeft, TRight> FirstOrOther<TLeft, TRight>(this IEnumerable<TRight> seq, Func<TRight, bool> predicate, TLeft whenEmpty) =>
            seq.FirstOrNone(predicate) is Some<TRight> some
                ? (Either<TLeft, TRight>)some.Content
                : whenEmpty;

        /// <summary>
        /// Returns the first element of a sequence of <typeparamref name="TRight"/> else the substitute <typeparamref name="TLeft"/> <paramref name="whenEmpty"/>
        /// </summary>
        public static Either<TLeft, TRight> FirstOrOther<TLeft, TRight>(this IEnumerable<TRight> seq, Func<TLeft> whenEmpty) =>
            seq.FirstOrNone() is Some<TRight> some
                ? (Either<TLeft, TRight>)some.Content
                : whenEmpty();

        /// <summary>
        /// Returns the first element of a sequence of <typeparamref name="TRight"/> else the substitute <typeparamref name="TLeft"/> <paramref name="whenEmpty"/>
        /// </summary>
        public static Either<TLeft, TRight> FirstOrOther<TLeft, TRight>(this IEnumerable<TRight> seq, Func<TRight, bool> predicate, Func<TLeft> whenEmpty) =>
            seq.FirstOrNone(predicate) is Some<TRight> some
                ? (Either<TLeft, TRight>)some.Content
                : whenEmpty();

        /// <summary>
        /// Applies a function to each element of the <typeparamref name="TRight"/> type if the <see cref="Either{TLeft, TRight}"/> is a <see cref="Right{TLeft, TRight}"/>
        /// </summary>
        public static IEnumerable<Either<TLeft, TNewRight>> Map<TLeft, TRight, TNewRight>(this IEnumerable<Either<TLeft, TRight>> seq, Func<TRight, TNewRight> map) =>
            seq.Select(either => either.Map(map));

        /// <summary>
        /// Reduces a sequence of <see cref="Either{TLeft, TRight}"/> to a sequence of <typeparamref name="TRight"/>
        /// by replacing each <typeparamref name="TLeft"/> with <paramref name="whenLeft"/>
        /// </summary>
        public static IEnumerable<TRight> Reduce<TLeft, TRight>(this IEnumerable<Either<TLeft, TRight>> seq, TRight whenLeft) =>
            seq.Select(e => e switch
            {
                Right<TLeft, TRight> right => right,
                _ => whenLeft
            });

        /// <summary>
        /// Reduces a sequence of <see cref="Either{TLeft, TRight}"/> to a sequence of <typeparamref name="TRight"/>
        /// by replacing each <typeparamref name="TLeft"/> with <paramref name="whenLeft"/>
        /// </summary>
        public static IEnumerable<TRight> Reduce<TLeft, TRight>(this IEnumerable<Either<TLeft, TRight>> seq, Func<TRight> whenLeft) =>
            seq.Select(e => e switch
            {
                Right<TLeft, TRight> right => right,
                _ => whenLeft()
            });

        /// <summary>
        /// Reduces a sequence of <see cref="Either{TLeft, TRight}"/> to a sequence of <typeparamref name="TRight"/>
        /// by replacing each <typeparamref name="TLeft"/> with <paramref name="whenLeft"/>
        /// </summary>
        public static IEnumerable<TRight> Reduce<TLeft, TRight>(this IEnumerable<Either<TLeft, TRight>> seq, Func<TLeft, TRight> whenleft) =>
            seq.Select(e => e switch
            {
                Right<TLeft, TRight> right => right,
                Left<TLeft, TRight> left => whenleft(left),
                _ => throw new Exception("We need discriminated unions")
            });
    }
}
