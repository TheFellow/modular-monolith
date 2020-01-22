namespace Functional.Either
{
    /// <summary>
    /// The <see cref="Left{TLeft, TRight}"/> instance
    /// </summary>
    /// <typeparam name="TLeft">The Left type</typeparam>
    /// <typeparam name="TRight">The Right type</typeparam>
    public sealed class Left<TLeft, TRight> : Either<TLeft, TRight>
    {
        public TLeft Content { get; }
        public Left(TLeft content) => Content = content;

        public static implicit operator TLeft(Left<TLeft, TRight> either) => either.Content;

        public override string ToString() => $"Left({Content})";
    }
}
