using System;
using System.Diagnostics.CodeAnalysis;

namespace Functional.Option
{
    /// <summary>
    /// <see cref="Option{T}"/> represents an instance of <typeparamref name="T"/>
    /// that may or may not be present
    /// </summary>
    /// <typeparam name="T">The underlying type</typeparam>
    public abstract class Option<T> : IEquatable<Option<T>>
    {
        public static implicit operator Option<T>(T some) => new Some<T>(some);
        public static implicit operator Option<T>(None _) => new None<T>();

        #region Equality Operators

        public static bool operator ==(Option<T> option, Some<T> some) => some.Equals(option);
        public static bool operator !=(Option<T> option, Some<T> some) => !some.Equals(option);
        public static bool operator ==(Some<T> some, Option<T> option) => some.Equals(option);
        public static bool operator !=(Some<T> some, Option<T> option) => !some.Equals(option);

        public static bool operator ==(Option<T> option, None<T> none) => none.Equals(option);
        public static bool operator !=(Option<T> option, None<T> none) => !none.Equals(option);
        public static bool operator ==(None<T> none, Option<T> option) => none.Equals(option);
        public static bool operator !=(None<T> none, Option<T> option) => !none.Equals(option);

        public static bool operator ==(Option<T> option, None none) => none.Equals(option);
        public static bool operator !=(Option<T> option, None none) => !none.Equals(option);
        public static bool operator ==(None none, Option<T> option) => none.Equals(option);
        public static bool operator !=(None none, Option<T> option) => !none.Equals(option);

        public static bool operator ==(Option<T> option1, Option<T> option2) => option1.Equals(option2);
        public static bool operator !=(Option<T> option1, Option<T> option2) => !option1.Equals(option2);

        #endregion

        public override bool Equals(object obj) => Equals(obj as Option<T>);
        public bool Equals([AllowNull] Option<T> other)
        {
            if (other is Some<T> some && this is Some<T> thisSome)
                return some.Equals(thisSome);

            if ((other is None || other is None<T>) && (this is None || this is None<T>))
                return true;

            return false;
        }

        public override int GetHashCode() =>
            this is Some<T> some
                ? some.GetHashCode()
                : None.Value.GetHashCode();


        /// <summary>
        /// Converts an <see cref="Option{T}"/> to an Option of the new type
        /// when T can be cast to TNew. None otherwise.
        /// </summary>
        /// <typeparam name="TNew">The new type of the <see cref="Option{T}"/></typeparam>
        /// <returns></returns>
        public Option<TNew> OfType<TNew>() where TNew : class =>
            this is Some<T> some && some.Content as TNew != null
                ? (Option<TNew>)new Some<TNew>((some.Content as TNew)!)
                : None.Value;
    }
}
