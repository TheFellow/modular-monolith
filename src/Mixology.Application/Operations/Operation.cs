namespace Mixology.Application.Operations;

public enum OperationKind
{
    Command,
    Query,
}

public readonly record struct Operation(OperationKind Kind, string Action)
{
    public static Operation Command(string action) => new(OperationKind.Command, action);
    public static Operation Query(string action) => new(OperationKind.Query, action);
}
