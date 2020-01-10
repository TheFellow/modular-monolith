using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ValueTypes
{
    public sealed class Value<T> : ValueBase
        where T : struct
    {
        public T Content { get; }
        public Value(T content) => this.Content = content;

        public override bool Equals([AllowNull] ValueBase other)
        {
            if (other is null) return false;
            if (other is Value<T> value) return this.Content.Equals(value.Content);
            return false;
        }
        
        public override int GetHashCode() => Content.GetHashCode();
        public override string ToString() => $"Value({Content})";
    }

    public abstract class Value : ValueBase
    {
        protected abstract IEnumerable<ValueBase> GetValues();
        public override bool Equals([AllowNull] ValueBase other)
        {
            if (other is null) return false;
            if (GetType() != other.GetType()) return false;
            return this.GetValues().SequenceEqual(((Value)other).GetValues());
        }

        public sealed override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var value in GetValues())
                hash.Add(value);
            return hash.ToHashCode();
        }
    }
}
