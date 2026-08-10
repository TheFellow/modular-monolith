using Expr.Syntax;

namespace Mixology.Filtering.Internal;

internal sealed class FilterCompatibilityRewriter<T>(FilterSchema<T> schema) : SyntaxRewriter
{
    private static readonly HashSet<string> LegacyStringOperators =
        ["contains", "startsWith", "endsWith", "matches"];

    protected override SyntaxNode VisitNode(SyntaxNode node)
    {
        if (node is IdentifierNode identifier && schema.TryGetField(identifier.Name, out _))
        {
            return Patch(identifier, new IdentifierNode(
                FilterSchema<T>.EnvironmentName(identifier.Name),
                identifier.Location));
        }

        if (node is MemberNode member && TryGetPath(member, out string? path) && schema.TryGetField(path, out _))
        {
            return Patch(member, new IdentifierNode(FilterSchema<T>.EnvironmentName(path), member.Location));
        }

        if (node is CallNode
            {
                Callee: MemberNode
                {
                    Target: SyntaxNode target,
                    Property: StringNode { Value: string operation },
                    IsMethod: true,
                },
                Arguments.Count: 1,
            } call && LegacyStringOperators.Contains(operation))
        {
            SyntaxNode argument = call.Arguments[0];
            return IsArrayField(target) && operation == "contains"
                ? Patch(call, new BinaryNode("in", argument, target, call.Location))
                : Patch(call, new BinaryNode(operation, target, argument, call.Location));
        }

        if (node is BinaryNode { Operator: "contains" } binary && IsArrayField(binary.Left))
        {
            return Patch(binary, new BinaryNode("in", binary.Right, binary.Left, binary.Location));
        }

        return node;
    }

    private bool IsArrayField(SyntaxNode node)
    {
        if (node is not IdentifierNode identifier)
        {
            return false;
        }

        return schema.Fields.Any(field =>
            FilterSchema<T>.EnvironmentName(field.Name) == identifier.Name && FilterTypeMapper.IsSequence(field.Type));
    }

    private static bool TryGetPath(MemberNode member, out string path)
    {
        if (member.Property is not StringNode property)
        {
            path = string.Empty;
            return false;
        }

        if (member.Target is IdentifierNode identifier)
        {
            path = $"{identifier.Name}.{property.Value}";
            return true;
        }

        if (member.Target is MemberNode parent && TryGetPath(parent, out string? parentPath))
        {
            path = $"{parentPath}.{property.Value}";
            return true;
        }

        path = string.Empty;
        return false;
    }
}
