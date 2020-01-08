namespace Functional.Either
{
    /// <summary>
    /// The <see cref="Right{TLeft, TRight}"/> instance
    /// </summary>
    /// <typeparam name="TLeft">The Left type</typeparam>
    /// <typeparam name="TRight">The Right type</typeparam>
    public sealed class Right<TLeft, TRight> : Either<TLeft, TRight>
    {
        public TRight Content { get; }
        public Right(TRight either) => Content = either;

        public static implicit operator TRight(Right<TLeft, TRight> either) => either.Content;

        public override string ToString() => $"Right({Content})";
    }
}
