using System.Linq.Expressions;

namespace Mixology.Filtering.Internal;

internal sealed class PushdownExpressionBuilder<TRow>(FilterPersistenceMap<TRow> map)
{
    public Expression<Func<TRow, bool>>? Build(IReadOnlyList<Pushdown> pushdowns)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TRow), "row");
        Expression? body = null;
        foreach (Pushdown pushdown in pushdowns)
        {
            if (!map.TryGet(pushdown.Field, out LambdaExpression selector))
            {
                continue;
            }

            Expression property = new ReplaceParameterVisitor(selector.Parameters[0], parameter).Visit(selector.Body)!;
            Expression predicate = BuildPredicate(property, pushdown);
            body = body is null ? predicate : Expression.AndAlso(body, predicate);
        }

        return body is null ? null : Expression.Lambda<Func<TRow, bool>>(body, parameter);
    }

    private static Expression BuildPredicate(Expression property, Pushdown pushdown)
    {
        Expression[] values = pushdown.Values.Select(value => Expression.Constant(value, property.Type)).ToArray();
        if (pushdown.Operator is "==" or "!=" && values.Length > 1)
        {
            Expression contains = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                [property.Type],
                Expression.NewArrayInit(property.Type, values),
                property);
            return pushdown.Operator == "==" ? contains : Expression.Not(contains);
        }

        Expression right = values[0];
        return pushdown.Operator switch
        {
            "==" => Expression.Equal(property, right),
            "!=" => Expression.NotEqual(property, right),
            ">" => Expression.GreaterThan(property, right),
            ">=" => Expression.GreaterThanOrEqual(property, right),
            "<" => Expression.LessThan(property, right),
            "<=" => Expression.LessThanOrEqual(property, right),
            _ => throw new InvalidOperationException($"Unsupported pushdown operator {pushdown.Operator}."),
        };
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression source, ParameterExpression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? replacement : base.VisitParameter(node);
    }
}

