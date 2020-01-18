using Functional.Either;
using System;

namespace Functional.Option
{
    public static class OptionExtensions
    {
        /// <summary>
        /// Reduces an <see cref="Option{T}"/> to its underlying type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">The underlying type</typeparam>
        /// <param name="option"></param>
        /// <param name="whenNone">Replacement value used when <paramref name="option"/> is <c>None</c></param>
        /// <returns><typeparamref name="T"/></returns>
        public static T Reduce<T>(this Option<T> option, T whenNone) =>
            option is Some<T> some
                ? some.Content
                : whenNone;

        /// <summary>
        /// Reduces an <see cref="Option{T}"/> to its underlying type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">The underlying type</typeparam>
        /// <param name="option"></param>
        /// <param name="whenNone">Function yielding the value when <paramref name="option"/> is <c>None</c></param>
        /// <returns><typeparamref name="T"/></returns>
        public static T Reduce<T>(this Option<T> option, Func<T> whenNone) =>
            option is Some<T> some
                ? some.Content
                : whenNone();

        /// <summary>
        /// Applies a function of <typeparamref name="T"/> to an <see cref="Option{T}"/>
        /// </summary>
        /// <typeparam name="T">The type <paramref name="map"/> receives</typeparam>
        /// <typeparam name="TNew">The type <paramref name="map"/> produces</typeparam>
        /// <param name="option"></param>
        /// <param name="map">A function mapping <typeparamref name="T"/> to <typeparamref name="TNew"/></param>
        public static Option<TNew> Map<T, TNew>(this Option<T> option, Func<T, TNew> map) =>
            option is Some<T> some
                ? (Option<TNew>)map(some.Content)
                : new None<TNew>();

        /// <summary>
        /// Applies a function of <typeparamref name="T"/> to an <see cref="Option{T}"/>
        /// </summary>
        /// <typeparam name="T">The type <paramref name="map"/> receives</typeparam>
        /// <typeparam name="TNew">The type <paramref name="map"/> produces</typeparam>
        /// <param name="option"></param>
        /// <param name="map">A function mapping <typeparamref name="T"/> to an <c>Option</c></param>
        public static Option<TNew> Map<T, TNew>(this Option<T> option, Func<T, Option<TNew>> map) =>
            option is Some<T> some
                ? map(some.Content)
                : new None<TNew>();

        /// <summary>
        /// Applies an <see cref="Action{T}"/> to the underlying value if present,
        /// returning the original <paramref name="option"/>
        /// </summary>
        /// <typeparam name="T">The underlying type</typeparam>
        /// <param name="option"></param>
        /// <param name="action">The action to perform <paramref name="option"/> if present</param>
        public static Option<T> Tee<T>(this Option<T> option, Action<T> action)
        {
            if (option is Some<T> some)
                action(some.Content);
            return option;
        }

        public static Either<TLeft, T> Else<TLeft, T>(this Option<T> option, TLeft whenNone) =>
            option is Some<T> some
                ? some.Content
                : (Either<TLeft, T>)whenNone;
    }
}
