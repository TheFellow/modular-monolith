using System.Collections;
using System.Text.RegularExpressions;

namespace Mixology.Filtering.Internal;

internal sealed class FilterEvaluator<T>(FilterSchema<T> schema)
{
    public bool Match(FilterNode node, T value) => (bool)Evaluate(node, value)!;

    private object? Evaluate(FilterNode node, T value) => node switch
    {
        LiteralNode literal => literal.Value,
        CallNode call => call.Value,
        FieldNode field => schema.RequireField(field.Name).Read(value),
        ListNode list => list.Items.Select(item => Evaluate(item, value)).ToArray(),
        UnaryNode unary when unary.Operator == "!" => !(bool)Evaluate(unary.Operand, value)!,
        BinaryNode binary => EvaluateBinary(binary, value),
        _ => throw new InvalidOperationException($"Unsupported filter node {node.GetType().Name}.")
    };

    private bool EvaluateBinary(BinaryNode binary, T value)
    {
        if (binary.Operator == "&&")
        {
            return (bool)Evaluate(binary.Left, value)! && (bool)Evaluate(binary.Right, value)!;
        }

        if (binary.Operator == "||")
        {
            return (bool)Evaluate(binary.Left, value)! || (bool)Evaluate(binary.Right, value)!;
        }

        object? left = Evaluate(binary.Left, value);
        object? right = Evaluate(binary.Right, value);
        return binary.Operator switch
        {
            "==" => Equals(left, right),
            "!=" => !Equals(left, right),
            "<" => Compare(left, right) < 0,
            "<=" => Compare(left, right) <= 0,
            ">" => Compare(left, right) > 0,
            ">=" => Compare(left, right) >= 0,
            "in" => ((IEnumerable)right!).Cast<object?>().Contains(left),
            "not in" => !((IEnumerable)right!).Cast<object?>().Contains(left),
            "contains" => Contains(left, right),
            "startsWith" => ((string)left!).StartsWith((string)right!, StringComparison.Ordinal),
            "endsWith" => ((string)left!).EndsWith((string)right!, StringComparison.Ordinal),
            "matches" => Regex.IsMatch((string)left!, (string)right!, RegexOptions.CultureInvariant),
            _ => throw new InvalidOperationException($"Unsupported filter operator {binary.Operator}.")
        };
    }

    private static bool Contains(object? left, object? right) => left switch
    {
        string text => text.Contains((string)right!, StringComparison.Ordinal),
        IEnumerable values => values.Cast<object?>().Contains(right),
        _ => false,
    };

    private static int Compare(object? left, object? right)
    {
        if (left is null || right is null)
        {
            throw new InvalidOperationException("Ordered comparison with null is not supported.");
        }

        return ((IComparable)left).CompareTo(right);
    }
}

