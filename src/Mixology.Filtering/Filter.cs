using System.Linq.Expressions;
using Mixology.Filtering.Internal;
using Mixology.Kernel.Errors;

namespace Mixology.Filtering;

public static class Filter
{
    public static FilterField<T> Field<T, TValue>(
        string name,
        Expression<Func<T, TValue>> read,
        string description = "")
    {
        Func<T, TValue> compiled = read.Compile();
        return new FilterField<T>(name, typeof(TValue), description, value => compiled(value));
    }

    public static PersistedFilterField<TRow> PersistedField<TRow, TValue>(
        string name,
        Expression<Func<TRow, TValue>> selector) => new(name, selector);

    public static FilterExpression<T>? Parse<T>(FilterSchema<T> schema, string? source)
    {
        source = source?.Trim();
        if (string.IsNullOrEmpty(source))
        {
            return null;
        }

        try
        {
            FilterNode parsed = new FilterParser(source).Parse();
            FilterNode checkedTree = new FilterTypeChecker<T>(schema).Check(parsed);
            return new FilterExpression<T>(source, schema, checkedTree);
        }
        catch (AppError)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw AppError.Invalid($"invalid filter: {exception.Message}", exception);
        }
    }
}
