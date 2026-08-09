using System.Linq.Expressions;

namespace Mixology.Filtering;

public sealed record FilterField<T>(
    string Name,
    Type Type,
    string Description,
    Func<T, object?> Read);

public sealed class FilterSchema<T>
{
    private readonly Dictionary<string, FilterField<T>> fields;

    public FilterSchema(IEnumerable<FilterField<T>> fields, params string[] examples)
    {
        this.fields = fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        Fields = this.fields.Values.OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
        Examples = examples;
    }

    public IReadOnlyList<FilterField<T>> Fields { get; }

    public IReadOnlyList<string> Examples { get; }

    public FilterField<T> RequireField(string name) => fields.TryGetValue(name, out FilterField<T>? field)
        ? field
        : throw Kernel.Errors.AppError.Invalid($"invalid filter: unknown field {name}");

}
