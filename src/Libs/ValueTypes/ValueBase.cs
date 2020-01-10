using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ValueTypes
{
    public abstract class ValueBase : IEquatable<ValueBase>
    {
        public abstract bool Equals([AllowNull] ValueBase other);
        public override bool Equals(object? obj) => this.Equals(obj as ValueBase);
        public abstract override int GetHashCode();

        public static bool operator ==(ValueBase a, ValueBase b) => a?.Equals(b) ?? false;
        public static bool operator !=(ValueBase a, ValueBase b) => !(a == b);

        public static implicit operator ValueBase(bool value) => new Value<bool>(value);
        public static implicit operator ValueBase(byte value) => new Value<byte>(value);
        public static implicit operator ValueBase(sbyte value) => new Value<sbyte>(value);
        public static implicit operator ValueBase(char value) => new Value<char>(value);
        public static implicit operator ValueBase(decimal value) => new Value<decimal>(value);
        public static implicit operator ValueBase(double value) => new Value<double>(value);
        public static implicit operator ValueBase(float value) => new Value<float>(value);
        public static implicit operator ValueBase(int value) => new Value<int>(value);
        public static implicit operator ValueBase(uint value) => new Value<uint>(value);
        public static implicit operator ValueBase(long value) => new Value<long>(value);
        public static implicit operator ValueBase(ulong value) => new Value<ulong>(value);
        public static implicit operator ValueBase(short value) => new Value<short>(value);
        public static implicit operator ValueBase(ushort value) => new Value<ushort>(value);

        public static implicit operator ValueBase(string value) => new ValueString(value);

        protected IEnumerable<ValueBase> Yield(params ValueBase[] values) => values;
        protected IEnumerable<ValueBase> Group(params ValueBase[] values) => new[] { new ValueGroup(values) };
    }
}
