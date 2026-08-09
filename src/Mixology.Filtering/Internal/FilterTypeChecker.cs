using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Mixology.Kernel.Errors;

namespace Mixology.Filtering.Internal;

internal sealed class FilterTypeChecker<T>(FilterSchema<T> schema)
{
    public FilterNode Check(FilterNode node)
    {
        (FilterNode checkedNode, Type type) = Visit(node);
        if (type != typeof(bool))
        {
            throw Invalid("expression must return bool");
        }

        return checkedNode;
    }

    private (FilterNode Node, Type Type) Visit(FilterNode node) => node switch
    {
        FieldNode field => (field, schema.RequireField(field.Name).Type),
        LiteralNode literal => (literal, literal.Value?.GetType() ?? typeof(object)),
        CallNode call => (call, call.Value.GetType()),
        ListNode list => CheckList(list),
        UnaryNode unary => CheckUnary(unary),
        BinaryNode binary => CheckBinary(binary),
        _ => throw Invalid($"unsupported node {node.GetType().Name}"),
    };

    private (FilterNode, Type) CheckList(ListNode list)
    {
        FilterNode[] items = new FilterNode[list.Items.Count];
        Type elementType = typeof(object);
        for (int index = 0; index < items.Length; index++)
        {
            (items[index], Type currentType) = Visit(list.Items[index]);
            if (index == 0)
            {
                elementType = currentType;
            }
            else if (currentType != elementType)
            {
                throw Invalid("list items must have one type");
            }
        }

        return (new ListNode(items), typeof(IEnumerable<>).MakeGenericType(elementType));
    }

    private (FilterNode, Type) CheckUnary(UnaryNode unary)
    {
        (FilterNode operand, Type type) = Visit(unary.Operand);
        if (type != typeof(bool))
        {
            throw Invalid("! requires a boolean operand");
        }

        return (unary with { Operand = operand }, typeof(bool));
    }

    private (FilterNode, Type) CheckBinary(BinaryNode binary)
    {
        if (binary.Operator is "&&" or "||")
        {
            (FilterNode left, Type leftType) = Visit(binary.Left);
            (FilterNode right, Type rightType) = Visit(binary.Right);
            if (leftType != typeof(bool) || rightType != typeof(bool))
            {
                throw Invalid($"{binary.Operator} requires boolean operands");
            }

            return (binary with { Left = left, Right = right }, typeof(bool));
        }

        if (binary.Left is FieldNode leftField)
        {
            Type target = schema.RequireField(leftField.Name).Type;
            return (binary with { Right = ConvertOperand(binary.Right, target, binary.Operator) }, typeof(bool));
        }

        if (binary.Right is FieldNode rightField)
        {
            Type target = schema.RequireField(rightField.Name).Type;
            return (binary with { Left = ConvertOperand(binary.Left, target, binary.Operator) }, typeof(bool));
        }

        (FilterNode _, Type leftTypeOther) = Visit(binary.Left);
        (FilterNode _, Type rightTypeOther) = Visit(binary.Right);
        if (leftTypeOther != rightTypeOther)
        {
            throw Invalid($"incompatible comparison: {leftTypeOther.Name} and {rightTypeOther.Name}");
        }

        return (binary, typeof(bool));
    }

    private FilterNode ConvertOperand(FilterNode node, Type target, string operation)
    {
        if (operation is "startsWith" or "endsWith" or "matches")
        {
            if (target != typeof(string) || node is not LiteralNode { Value: string })
            {
                throw Invalid($"{operation} requires strings");
            }

            if (operation == "matches" && node is LiteralNode { Value: string pattern })
            {
                try
                {
                    _ = new Regex(pattern, RegexOptions.CultureInvariant);
                }
                catch (ArgumentException exception)
                {
                    throw Invalid($"invalid regular expression: {exception.Message}");
                }
            }

            return node;
        }

        if (operation is "in" or "not in")
        {
            if (node is not ListNode list)
            {
                throw Invalid($"{operation} requires a list");
            }

            return new ListNode(list.Items.Select(item => ConvertScalar(item, target)).ToArray());
        }

        if (operation == "contains" && target != typeof(string) && typeof(IEnumerable).IsAssignableFrom(target))
        {
            Type elementType = target.IsArray
                ? target.GetElementType()!
                : target.GetInterfaces().Append(target)
                    .First(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    .GetGenericArguments()[0];
            return ConvertScalar(node, elementType);
        }

        return ConvertScalar(node, target);
    }

    private FilterNode ConvertScalar(FilterNode node, Type target)
    {
        if (node is CallNode call)
        {
            if (target == call.Value.GetType() || (target == typeof(DateTime) && call.Value is DateTimeOffset))
            {
                return target == typeof(DateTime) && call.Value is DateTimeOffset date
                    ? call with { Value = date.UtcDateTime }
                    : call;
            }

            throw Invalid($"cannot compare {call.Name} with {target.Name}");
        }

        if (node is not LiteralNode literal)
        {
            (FilterNode visited, Type type) = Visit(node);
            if (type != target)
            {
                throw Invalid($"expected {target.Name}, got {type.Name}");
            }

            return visited;
        }

        if (literal.Value is null)
        {
            return !target.IsValueType || Nullable.GetUnderlyingType(target) is not null
                ? literal
                : throw Invalid($"null is not valid for {target.Name}");
        }

        Type nonNullable = Nullable.GetUnderlyingType(target) ?? target;
        try
        {
            object converted = nonNullable == typeof(string)
                ? literal.Value is string text ? text : throw Invalid("expected string literal")
                : Convert.ChangeType(literal.Value, nonNullable, CultureInfo.InvariantCulture);
            return new LiteralNode(converted);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            throw Invalid($"cannot convert literal to {target.Name}");
        }
    }

    private static AppError Invalid(string message) => AppError.Invalid($"invalid filter: {message}");
}
