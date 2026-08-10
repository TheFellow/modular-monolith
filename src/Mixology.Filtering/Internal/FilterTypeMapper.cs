using Expr.Types;

namespace Mixology.Filtering.Internal;

internal static class FilterTypeMapper
{
    internal static bool IsSequence(Type type) => TryGetSequenceElementType(type, out _);

    internal static ExprTypeDescriptor Map(Type type)
    {
        Type actual = Nullable.GetUnderlyingType(type) ?? type;
        ExprTypeDescriptor descriptor;
        if (actual == typeof(bool))
        {
            descriptor = ExprTypes.Boolean;
        }
        else if (actual == typeof(string))
        {
            descriptor = ExprTypes.String;
        }
        else if (ExprTypes.IsIntegral(actual))
        {
            descriptor = ExprTypes.Integer;
        }
        else if (actual == typeof(float) || actual == typeof(double) || actual == typeof(Half))
        {
            descriptor = ExprTypes.Float;
        }
        else if (actual == typeof(DateTime) || actual == typeof(DateTimeOffset))
        {
            descriptor = ExprTypes.Time;
        }
        else if (actual == typeof(TimeSpan))
        {
            descriptor = ExprTypes.Duration;
        }
        else if (TryGetSequenceElementType(actual, out Type? elementType))
        {
            descriptor = ExprTypes.ArrayOf(Map(elementType!));
        }
        else
        {
            throw new NotSupportedException($"Filter fields of type {type} are not supported.");
        }

        return Nullable.GetUnderlyingType(type) is null ? descriptor : ExprTypes.Nullable(descriptor);
    }

    private static bool TryGetSequenceElementType(Type type, out Type? elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return elementType is not null;
        }

        Type? sequenceType = type.GetInterfaces().Append(type).FirstOrDefault(candidate =>
            candidate.IsGenericType
            && candidate.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
        elementType = sequenceType?.GetGenericArguments()[0];
        return elementType is not null;
    }
}
