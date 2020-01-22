using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Functional.Option
{
    /// <summary>
    /// Represents an instance of <typeparamref name="T"/> which is present
    /// </summary>
    /// <typeparam name="T">The underlying type</typeparam>
    public sealed class Some<T> : Option<T>, IEquatable<Some<T>>
    {
        public T Content { get; }
        public Some(T Content) => this.Content = Content;

        public static implicit operator T(Some<T> some) => some.Content;
        public static implicit operator Some<T>(T content) => new Some<T>(content);

        public override string ToString() => $"Some({Content})";
        public bool Equals([AllowNull] Some<T> other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<T>.Default.Equals(Content, other.Content);
        }
        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Some<T> some && Equals(some);
        }

        public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(Content);

        public static bool operator ==(Some<T> a, Some<T> b) => a is null && b is null || !(a is null) && a.Equals(b);
        public static bool operator !=(Some<T> a, Some<T> b) => !(a == b);
    }
}
