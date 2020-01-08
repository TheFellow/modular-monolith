namespace Functional.Option
{
    /// <summary>
    /// <see cref="Option{T}"/> represents an instance of <typeparamref name="T"/>
    /// that may or may not be present
    /// </summary>
    /// <typeparam name="T">The underlying type</typeparam>
    public abstract class Option<T>
    {
        public static implicit operator Option<T>(T some) => new Some<T>(some);
        public static implicit operator Option<T>(None _) => new None<T>();

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
