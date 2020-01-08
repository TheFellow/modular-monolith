namespace Functional.Either
{
    /// <summary>
    /// Represents a value that is one of two types <typeparamref name="TLeft"/> or <typeparamref name="TRight"/>
    /// </summary>
    /// <typeparam name="TLeft">The left type</typeparam>
    /// <typeparam name="TRight">The right type</typeparam>
    public abstract class Either<TLeft, TRight>
    {
        public static implicit operator Either<TLeft, TRight>(TLeft obj) =>
            new Left<TLeft, TRight>(obj);

        public static implicit operator Either<TLeft, TRight>(TRight obj) =>
            new Right<TLeft, TRight>(obj);
    }
}
