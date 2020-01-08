using System.Collections.Generic;

namespace Functional.Option
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Returns <see cref="Some{T}"/> if the key is found else <see cref="None{T}"/>
        /// </summary>
        /// <typeparam name="TKey">The type of the key</typeparam>
        /// <typeparam name="TValue">The type of the value</typeparam>
        /// <param name="dict"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static Option<TValue> TryGetValue<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
            where TKey : notnull =>
            dict.TryGetValue(key, out TValue value)
                ? (Option<TValue>)new Some<TValue>(value)
                : None.Value;
    }
}
