using Expr;
using Expr.Syntax;

namespace Mixology.Filtering.Internal;

internal static class FilterConstantValidator
{
    internal static void Validate(SyntaxNode root)
    {
        foreach (SyntaxNode node in SyntaxWalker.Traverse(root))
        {
            if (node is CallNode
                {
                    Callee: IdentifierNode { Name: "date" or "duration" },
                    Arguments: var arguments,
                } call && arguments.All(IsLiteral))
            {
                _ = ExprEngine.Evaluate(SyntaxPrinter.Print(call));
            }

            if (node is BuiltinNode { Name: "date" or "duration", Arguments: var builtinArguments } builtin
                && builtinArguments.All(IsLiteral))
            {
                _ = ExprEngine.Evaluate(SyntaxPrinter.Print(builtin));
            }

            if (node is BinaryNode { Operator: "matches", Right: StringNode pattern })
            {
                BinaryNode probe = new(
                    "matches",
                    new StringNode(string.Empty, node.Location),
                    pattern,
                    node.Location);
                _ = ExprEngine.Evaluate(SyntaxPrinter.Print(probe));
            }
        }
    }

    private static bool IsLiteral(SyntaxNode node) =>
        node is NilNode or BooleanNode or IntegerNode or FloatNode or StringNode;
}
