namespace Mixology.Kernel.Paging;

public readonly record struct Cursor
{
    private readonly string? value;

    public Cursor(string? value)
    {
        this.value = value;
    }

    public string Value => value ?? string.Empty;

    public bool IsEmpty => string.IsNullOrEmpty(value);

    public override string ToString() => Value;

    public static implicit operator Cursor(string? value) => new(value);
}

