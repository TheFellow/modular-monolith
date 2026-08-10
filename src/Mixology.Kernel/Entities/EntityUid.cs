namespace Mixology.Kernel.Entities;

public readonly record struct EntityUid(string Type, string Id)
{
    public bool IsEmpty => string.IsNullOrEmpty(Id);

    public override string ToString() => Id ?? string.Empty;
}

public interface IEntityId
{
    string Value { get; }

    EntityUid EntityUid { get; }

    bool IsEmpty { get; }
}

