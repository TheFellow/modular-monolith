namespace Mixology.Filtering;

public abstract record FilterNode;

public sealed record LiteralNode(object? Value) : FilterNode;

public sealed record FieldNode(string Name) : FilterNode;

public sealed record ListNode(IReadOnlyList<FilterNode> Items) : FilterNode;

public sealed record CallNode(string Name, object Value, LiteralNode Argument) : FilterNode;

public sealed record UnaryNode(string Operator, FilterNode Operand) : FilterNode;

public sealed record BinaryNode(string Operator, FilterNode Left, FilterNode Right) : FilterNode;

