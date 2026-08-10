namespace Mixology.Filtering.Internal;

internal sealed record Pushdown(string Field, string Operator, IReadOnlyList<object> Values);

internal sealed class PushdownPlanner<T>(FilterSchema<T> schema)
{
    public IReadOnlyList<Pushdown> Plan(FilterNode node) => Implied(node, negated: false);

    private IReadOnlyList<Pushdown> Implied(FilterNode node, bool negated)
    {
        if (node is UnaryNode { Operator: "!" } unary)
        {
            return Implied(unary.Operand, !negated);
        }

        if (node is BinaryNode { Operator: "&&" or "||" } logical)
        {
            string operation = logical.Operator;
            if (negated)
            {
                operation = operation == "&&" ? "||" : "&&";
            }

            IReadOnlyList<Pushdown> left = Implied(logical.Left, negated);
            IReadOnlyList<Pushdown> right = Implied(logical.Right, negated);
            return operation == "&&" ? Conjunction(left, right) : Disjunction(left, right);
        }

        if (node is FieldNode booleanField && schema.RequireField(booleanField.Name).Type == typeof(bool))
        {
            return [new Pushdown(booleanField.Name, "==", [!negated])];
        }

        if (TryComparison(node, negated, out Pushdown? comparison))
        {
            return [comparison!];
        }

        return [];
    }

    private static bool TryComparison(FilterNode node, bool negated, out Pushdown? result)
    {
        result = null;
        if (node is not BinaryNode binary || binary.Operator is "&&" or "||")
        {
            return false;
        }

        string operation = binary.Operator;
        if (negated && !TryNegate(operation, out operation))
        {
            return false;
        }

        FilterNode left = binary.Left;
        FilterNode right = binary.Right;
        if (left is not FieldNode && right is FieldNode)
        {
            (left, right) = (right, left);
            operation = Reverse(operation);
        }

        if (left is not FieldNode field)
        {
            return false;
        }

        object[] values;
        if (right is LiteralNode { Value: not null } literal && operation is "==" or "!=" or ">" or ">=" or "<" or "<=")
        {
            values = [literal.Value];
        }
        else if (right is CallNode call && operation is "==" or "!=" or ">" or ">=" or "<" or "<=")
        {
            values = [call.Value];
        }
        else if (right is ListNode list && operation is "in" or "not in"
            && list.Items.All(item => item is LiteralNode { Value: not null }))
        {
            values = list.Items.Cast<LiteralNode>().Select(item => item.Value!).Distinct().ToArray();
            operation = operation == "in" ? "==" : "!=";
        }
        else
        {
            return false;
        }

        if (values.Length == 0)
        {
            return false;
        }

        result = new Pushdown(field.Name, operation, values);
        return true;
    }

    private static List<Pushdown> Conjunction(IReadOnlyList<Pushdown> left, IReadOnlyList<Pushdown> right)
    {
        List<Pushdown> result = [];
        foreach (Pushdown candidate in left.Concat(right))
        {
            int merge = result.FindIndex(item => item.Field == candidate.Field && item.Operator == candidate.Operator
                && candidate.Operator == "!=" && Compatible(item.Values, candidate.Values));
            if (merge >= 0)
            {
                result[merge] = result[merge] with { Values = result[merge].Values.Concat(candidate.Values).Distinct().ToArray() };
            }
            else if (!result.Any(item => Equivalent(item, candidate)))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private static List<Pushdown> Disjunction(IReadOnlyList<Pushdown> left, IReadOnlyList<Pushdown> right)
    {
        List<Pushdown> result = left.Where(candidate => right.Any(other => Equivalent(candidate, other))).ToList();
        foreach (string field in left.Where(item => item.Operator == "==").Select(item => item.Field).Distinct())
        {
            object[] leftValues = left.Where(item => item.Field == field && item.Operator == "==").SelectMany(item => item.Values).Distinct().ToArray();
            object[] rightValues = right.Where(item => item.Field == field && item.Operator == "==").SelectMany(item => item.Values).Distinct().ToArray();
            if (leftValues.Length > 0 && rightValues.Length > 0 && Compatible(leftValues, rightValues))
            {
                Pushdown widened = new(field, "==", leftValues.Concat(rightValues).Distinct().ToArray());
                if (!result.Any(item => Equivalent(item, widened)))
                {
                    result.Add(widened);
                }
            }
        }

        return result;
    }

    private static bool Equivalent(Pushdown left, Pushdown right) =>
        left.Field == right.Field && left.Operator == right.Operator
        && (left.Operator is "==" or "!="
            ? left.Values.Count == right.Values.Count && left.Values.All(right.Values.Contains)
            : left.Values.SequenceEqual(right.Values));

    private static bool Compatible(IReadOnlyList<object> left, IReadOnlyList<object> right) =>
        left.Count > 0 && right.Count > 0 && left.Concat(right).All(value => value.GetType() == left[0].GetType());

    private static bool TryNegate(string operation, out string negated)
    {
        negated = operation switch
        {
            "==" => "!=",
            "!=" => "==",
            ">" => "<=",
            ">=" => "<",
            "<" => ">=",
            "<=" => ">",
            "in" => "not in",
            "not in" => "in",
            _ => string.Empty,
        };
        return negated.Length > 0;
    }

    private static string Reverse(string operation) => operation switch
    {
        ">" => "<",
        ">=" => "<=",
        "<" => ">",
        "<=" => ">=",
        _ => operation,
    };
}
