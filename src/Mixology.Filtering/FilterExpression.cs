using Expr;
using Expr.Syntax;
using Mixology.Filtering.Internal;
using Mixology.Kernel.Errors;

namespace Mixology.Filtering;

public sealed class FilterExpression<T>
{
    private readonly CompiledExpression expression;

    internal FilterExpression(
        string source,
        FilterSchema<T> schema,
        SyntaxNode tree,
        CompiledExpression expression)
    {
        Source = source;
        Schema = schema;
        Tree = tree;
        Canonical = SyntaxPrinter.Print(tree);
        this.expression = expression;
    }

    public string Source { get; }

    public string Canonical { get; }

    public FilterSchema<T> Schema { get; }

    public SyntaxNode Tree { get; }

    public bool Match(T value)
    {
        try
        {
            return expression.Run(value) is true;
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

    public System.Linq.Expressions.Expression<Func<TRow, bool>>? BuildPushdown<TRow>(FilterPersistenceMap<TRow> map)
    {
        IReadOnlyList<Pushdown> pushdowns = new PushdownPlanner<T>(Schema).Plan(expression.SyntaxTree.Root);
        return new Internal.PushdownExpressionBuilder<TRow>(map).Build(pushdowns);
    }

    public override string ToString() => Canonical;
}
