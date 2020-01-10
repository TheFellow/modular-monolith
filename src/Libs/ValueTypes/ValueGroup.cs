using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ValueTypes
{
    public sealed class ValueGroup : ValueBase, IEquatable<ValueGroup>
    {
        private ValueBase[] Values { get; }
        public ValueGroup(ValueBase[] values) => Values = values;
        public bool Equals([AllowNull] ValueGroup other)
        {
            var counts = Values
                .GroupBy(v => v)
                .ToDictionary(g => g.Key, g => g.Count());
            bool ok = true;
            foreach (var value in other.Values)
            {
                if (counts.TryGetValue(value, out int c))
                    counts[value] = c - 1;
                else
                {
                    ok = false;
                    break;
                }
            }
            return ok && counts.Values.All(c => c == 0);
        }

        public override bool Equals([AllowNull] ValueBase other) => this.Equals(other as ValueGroup);
        public override int GetHashCode()
        {
            var hash = new HashCode();

            var hashValues = Values
                .Select(v => v.GetHashCode())
                .OrderBy(h => h);

            foreach (var item in hashValues)
                hash.Add(item);

            return hash.ToHashCode();
        }
    }
}
