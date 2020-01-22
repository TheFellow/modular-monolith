using System;
using System.Collections.Generic;
using System.Linq;

namespace Functional.Option
{
    public static class OptionEnumerableExtensions
    {
        /// <summary>
        /// Applies a function returning an <see cref="Option{T}"/> to a sequence and returns the sequence
        /// of Content from the elements which return <see cref="Some{T}"/>
        /// </summary>
        /// <typeparam name="T">The type of the sequence</typeparam>
        /// <typeparam name="TResult">The type of the Option the Func returns</typeparam>
        /// <param name="sequence"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static IEnumerable<TResult> Flatten<T, TResult>(this IEnumerable<T> sequence, Func<T, Option<TResult>> map) =>
            sequence.Select(map)
                .OfType<Some<TResult>>()
                .Select(s => s.Content);

        /// <summary>
        /// Returns a sequence of Content from a sequence of <see cref="Option{T}"/>s
        /// </summary>
        /// <typeparam name="T">The underlying type</typeparam>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static IEnumerable<T> Flatten<T>(this IEnumerable<Option<T>> sequence) =>
            sequence
            .OfType<Some<T>>()
            .Select(s => s.Content);

        /// <summary>
        /// Returns the first element of the sequence as a <see cref="Some{T}"/> else <see cref="None{T}"/>
        /// </summary>
        /// <typeparam name="T">The underlying type</typeparam>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static Option<T> FirstOrNone<T>(this IEnumerable<T> sequence) =>
            sequence
                .Select(o => (Option<T>)new Some<T>(o))
                .DefaultIfEmpty(None.Value)
                .First();

        /// <summary>
        /// Returns the first element of a sequence matching the <paramref name="predicate"/> as a <see cref="Some{T}"/>
        /// else <see cref="None{T}"/>
        /// </summary>
        /// <typeparam name="T">The underlying type</typeparam>
        /// <param name="sequence"></param>
        /// <param name="predicate">Predicate determining which element to select</param>
        /// <returns></returns>
        public static Option<T> FirstOrNone<T>(this IEnumerable<T> sequence, Func<T, bool> predicate) =>
            sequence
                .Where(predicate)
                .FirstOrNone();
    }
}
