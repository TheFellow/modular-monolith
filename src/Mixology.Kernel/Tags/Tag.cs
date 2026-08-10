using System.Text;
using Mixology.Kernel.Errors;

namespace Mixology.Kernel.Tags;

public readonly record struct Tag(string Key, string Value = "")
{
    public const int MaxKeyLength = 64;
    public const int MaxValueLength = 256;

    public static Tag Create(string key, string value = "")
    {
        Tag tag = new(key.Trim(), value.Trim());
        tag.Validate();
        return tag;
    }

    public static Tag Parse(string value)
    {
        int separator = value.IndexOf('=', StringComparison.Ordinal);
        return separator < 0
            ? Create(value)
            : Create(value[..separator], value[(separator + 1)..]);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw AppError.Invalid("tag key is required");
        }

        if (Key != Key.Trim())
        {
            throw AppError.Invalid("tag key must not have outer whitespace");
        }

        if (Key.Contains('=', StringComparison.Ordinal))
        {
            throw AppError.Invalid("tag key must not contain =");
        }

        if (Key.Any(char.IsControl) || Value.Any(char.IsControl))
        {
            throw AppError.Invalid("tag must not contain control characters");
        }

        if (Value != Value.Trim())
        {
            throw AppError.Invalid("tag value must not have outer whitespace");
        }

        if (Key.EnumerateRunes().Count() > MaxKeyLength)
        {
            throw AppError.Invalid($"tag key must be at most {MaxKeyLength} characters");
        }

        if (Value.EnumerateRunes().Count() > MaxValueLength)
        {
            throw AppError.Invalid($"tag value must be at most {MaxValueLength} characters");
        }
    }

    public override string ToString() => string.IsNullOrEmpty(Value) ? Key : $"{Key}={Value}";
}
