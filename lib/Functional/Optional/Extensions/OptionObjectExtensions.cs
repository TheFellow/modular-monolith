using System;

namespace Functional.Option
{
    public static class OptionObjectExtensions
    {
        /// <summary>
        /// Converts an instance of <typeparamref name="T"/> to 
        /// <see cref="Some{T}"/> if non-null, <see cref="None{T}"/> otherwise.
        /// </summary>
        /// <typeparam name="T">The underlying type</typeparam>
        public static Option<T> NoneIfNull<T>(this T obj) =>
            obj.When(!(obj is null));

        /// <summary>
        /// Applies a predicate to an instance of <typeparamref name="T"/>
        /// returning <see cref="Some{T}"/> when the predicate is true,
        /// <see cref="None{T}"/> otherwise
        /// </summary>
        /// <typeparam name="T">The underlying type</typeparam>
        /// <param name="predicate">Flag indicating if <c>When</c> should yield <c>Some</c> or <c>None</c></param>
        public static Option<T> When<T>(this T obj, bool predicate) =>
            predicate ? (Option<T>)obj : None.Value;

        /// <summary>
        /// Applies a predicate to an instance of <typeparamref name="T"/>
        /// returning <see cref="Some{T}"/> when the predicate is true,
        /// <see cref="None{T}"/> otherwise
        /// </summary>
        /// <typeparam name="T">The underlying type</typeparam>
        /// <param name="predicate"><c>Func</c> indicating if <c>When</c> should yield <c>Some</c> or <c>None</c></param>
        public static Option<T> When<T>(this T obj, Func<bool> predicate) =>
            predicate() ? (Option<T>)obj : None.Value;

        /// <summary>
        /// Applies a predicate to an instance of <typeparamref name="T"/>
        /// returning <see cref="Some{T}"/> when the predicate is true,
        /// <see cref="None{T}"/> otherwise
        /// </summary>
        /// <typeparam name="T">The underlying type</typeparam>
        /// <param name="predicate"><c>Func</c> indicating if <c>When</c> should yield <c>Some</c> or <c>None</c></param>
        public static Option<T> When<T>(this T obj, Func<T, bool> predicate) =>
            predicate(obj) ? (Option<T>)obj : None.Value;
    }
}
