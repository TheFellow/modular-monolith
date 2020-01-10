using System.Diagnostics.CodeAnalysis;

namespace ValueTypes
{
    public sealed class ValueString : ValueBase
    {
        public string? Content { get; }
        public ValueString(string? content) => Content = content;

        public override bool Equals([AllowNull] ValueBase other)
        { 
            if (other is null) return false;
            if (other is ValueString value) return this.Content?.Equals(value.Content) ?? (Content is null && value.Content is null);
            return false;
        }

        public override int GetHashCode() => this.Content?.GetHashCode() ?? 0;

        public override string ToString() => $"Value(\"{Content}\")";
    }
}
