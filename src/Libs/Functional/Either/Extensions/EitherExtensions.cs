using Functional.Option;
using System;

namespace Functional.Either
{
    public static class EitherExtensions
    {
        /// <summary>
        /// Reduces an <see cref="Either{TLeft, TRight}"/> to the <typeparamref name="TRight"/> type
        /// by providing a substitute value to use <paramref name="whenLeft"/>
        /// </summary>
        public static TRight Reduce<TLeft, TRight>(this Either<TLeft, TRight> either, TRight whenLeft) =>
            either is Right<TLeft, TRight> right ? right.Content : whenLeft;

        /// <summary>
        /// Reduces an <see cref="Either{TLeft, TRight}"/> to the <typeparamref name="TRight"/> type
        /// by providing a substitute value to use <paramref name="whenLeft"/>
        /// </summary>
        public static TRight Reduce<TLeft, TRight>(this Either<TLeft, TRight> either, Func<TRight> whenLeft) =>
            either is Right<TLeft, TRight> right ? right.Content : whenLeft();

        /// <summary>
        /// Reduces an <see cref="Either{TLeft, TRight}"/> to the <typeparamref name="TRight"/> type
        /// by providing a substitute value to use <paramref name="whenLeft"/>
        /// </summary>
        public static TRight Reduce<TLeft, TRight>(this Either<TLeft, TRight> either, Func<TLeft, TRight> whenleft) =>
            either is Right<TLeft, TRight> right ? right.Content : whenleft((Left<TLeft, TRight>)either);

        /// <summary>
        /// Applies a function to the <typeparamref name="TRight"/> type if the <see cref="Either{TLeft, TRight}"/> is a <see cref="Right{TLeft, TRight}"/>
        /// </summary>
        public static Either<TLeft, TNewRight> Map<TLeft, TRight, TNewRight>(this Either<TLeft, TRight> either, Func<TRight, TNewRight> map) =>
            either is Right<TLeft, TRight> right
                ? (Either<TLeft, TNewRight>)map(right.Content)
                : (TLeft)(Left<TLeft, TRight>)either;

        /// <summary>
        /// Applies a function to the <typeparamref name="TRight"/> type if the <see cref="Either{TLeft, TRight}"/> is a <see cref="Right{TLeft, TRight}"/>
        /// </summary>
        public static Either<TLeft, TNewRight> Map<TLeft, TRight, TNewRight>(this Either<TLeft, TRight> either, Func<TRight, Either<TLeft, TNewRight>> map) =>
            either is Right<TLeft, TRight> right
                ? map(right)
                : (TLeft)(Left<TLeft, TRight>)either;

        /// <summary>
        /// Applies a function to the <typeparamref name="TLeft"/> type if the <see cref="Either{TLeft, TRight}"/> is a <see cref="Left{TLeft, TRight}"/>
        /// </summary>
        public static Either<TNewLeft, TRight> MapLeft<TLeft, TNewLeft, TRight>(this Either<TLeft, TRight> either, Func<TLeft, TNewLeft> map) =>
            either is Left<TLeft, TRight> left
                ? (Either<TNewLeft, TRight>)map(left.Content)
                : (TRight)(Right<TLeft, TRight>)either;

        /// <summary>
        /// Applies a function to the <typeparamref name="TLeft"/> type if the <see cref="Either{TLeft, TRight}"/> is a <see cref="Left{TLeft, TRight}"/>
        /// </summary>
        public static Either<TNewLeft, TRight> MapLeft<TLeft, TNewLeft, TRight>(this Either<TLeft, TRight> either, Func<TLeft, Either<TNewLeft, TRight>> map) =>
            either is Left<TLeft, TRight> left
                ? (Either<TNewLeft, TRight>)map(left)
                : (TRight)(Right<TLeft, TRight>)either;

        /// <summary>
        /// Applies one of two functions to the <see cref="Either{TLeft, TRight}"/>. <paramref name="rightMap"/> when
        /// <see cref="Right{TLeft, TRight}"/> and <paramref name="leftMap"/> when <see cref="Left{TLeft, TRight}"/>
        /// </summary>
        public static Either<TNewLeft, TNewRight> Maps<TLeft, TRight, TNewLeft, TNewRight>(this Either<TLeft, TRight> either, Func<TRight, TNewRight> rightMap, Func<TLeft, TNewLeft> leftMap) =>
            either.Map(rightMap).MapLeft(leftMap);

        /// <summary>
        /// Calls an <see cref="Action{T}"/> when the <see cref="Either{TLeft, TRight}"/> is a <see cref="Right{TLeft, TRight}"/> and
        /// returns the original <see cref="Either{TLeft, TRight}"/>
        /// </summary>
        public static Either<TLeft, TRight> Tee<TLeft, TRight>(this Either<TLeft, TRight> either, Action<TRight> action)
        {
            if (either is Right<TLeft, TRight> right)
                action(right.Content);
            return either;
        }
    }
}
