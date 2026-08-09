using Mixology.Filtering.Internal;
using Mixology.Kernel.Errors;

namespace Mixology.Filtering;

public sealed class FilterExpression<T>
{
    private readonly FilterEvaluator<T> evaluator;

    internal FilterExpression(string source, FilterSchema<T> schema, FilterNode tree)
    {
        Source = source;
        Schema = schema;
        Tree = tree;
        Canonical = FilterFormatter.Format(tree);
        evaluator = new FilterEvaluator<T>(schema);
    }

    public string Source { get; }

    public string Canonical { get; }

    public FilterSchema<T> Schema { get; }

    public FilterNode Tree { get; }

    public bool Match(T value) => evaluator.Match(Tree, value);

    public System.Linq.Expressions.Expression<Func<TRow, bool>>? BuildPushdown<TRow>(FilterPersistenceMap<TRow> map)
    {
        IReadOnlyList<Internal.Pushdown> pushdowns = new Internal.PushdownPlanner<T>(Schema).Plan(Tree);
        return new Internal.PushdownExpressionBuilder<TRow>(map).Build(pushdowns);
    }

    public override string ToString() => Canonical;
}
