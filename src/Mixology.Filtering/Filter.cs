using System.Linq.Expressions;
using Expr;
using Expr.Configuration;
using Expr.Syntax;
using Expr.Types;
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
        ExprTypeDescriptor type = FilterTypeMapper.Map(typeof(TValue));
        string environmentName = FilterSchema<T>.EnvironmentName(name);
        return new FilterField<T>(
            name,
            typeof(TValue),
            description,
            value => compiled(value),
            builder => builder.Member(environmentName, compiled, type));
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
            SyntaxTree parsed = ExprEngine.Parse(source);
            SyntaxNode compatible = new Internal.FilterCompatibilityRewriter<T>(schema).Visit(parsed.Root);
            FilterConstantValidator.Validate(compatible);
            SyntaxTree rewritten = new(compatible, parsed.Source);
            ExprConfiguration configuration = ExprConfiguration.Default
                .WithEnvironment(schema.Environment)
                .WithExpectedType(ExprTypes.Boolean, warnOnAny: true);
            CompiledExpression compiled = ExprEngine.Compile(rewritten, configuration);
            return new FilterExpression<T>(source, schema, parsed.Root, compiled);
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
