using System.Linq.Expressions;

namespace Mixology.Filtering;

public sealed record PersistedFilterField<TRow>(string Name, LambdaExpression Selector);

public sealed class FilterPersistenceMap<TRow>
{
    private readonly Dictionary<string, LambdaExpression> selectors;

    public FilterPersistenceMap(IEnumerable<PersistedFilterField<TRow>> fields)
    {
        selectors = fields.ToDictionary(field => field.Name, field => field.Selector, StringComparer.Ordinal);
    }

    internal bool TryGet(string name, out LambdaExpression selector) => selectors.TryGetValue(name, out selector!);
}

