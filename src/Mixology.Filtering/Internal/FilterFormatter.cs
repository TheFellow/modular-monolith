using System.Globalization;
using System.Text.Json;

namespace Mixology.Filtering.Internal;

internal static class FilterFormatter
{
    public static string Format(FilterNode node, int parentPrecedence = 0) => node switch
    {
        LiteralNode literal => FormatLiteral(literal.Value),
        FieldNode field => field.Name,
        ListNode list => $"[{string.Join(", ", list.Items.Select(item => Format(item)))}]",
        CallNode call => $"{call.Name}({Format(call.Argument)})",
        UnaryNode unary => Parenthesize($"!{Format(unary.Operand, 4)}", 4, parentPrecedence),
        BinaryNode binary => FormatBinary(binary, parentPrecedence),
        _ => throw new InvalidOperationException($"Unsupported filter node {node.GetType().Name}.")
    };

    private static string FormatBinary(BinaryNode binary, int parentPrecedence)
    {
        if (binary.Operator is "contains" or "startsWith" or "endsWith" or "matches")
        {
            return $"{Format(binary.Left, 4)}.{binary.Operator}({Format(binary.Right)})";
        }

        int precedence = binary.Operator == "||" ? 1 : binary.Operator == "&&" ? 2 : 3;
        string text = $"{Format(binary.Left, precedence)} {binary.Operator} {Format(binary.Right, precedence + 1)}";
        return Parenthesize(text, precedence, parentPrecedence);
    }

    private static string FormatLiteral(object? value) => value switch
    {
        null => "nil",
        string text => JsonSerializer.Serialize(text),
        bool boolean => boolean ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static string Parenthesize(string value, int precedence, int parentPrecedence) =>
        precedence < parentPrecedence ? $"({value})" : value;
}

