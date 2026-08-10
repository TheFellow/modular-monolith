using System.Text;
using Expr.Runtime;

namespace Mixology.Filtering;

public sealed record FilterField<T>(
    string Name,
    Type Type,
    string Description,
    Func<T, object?> Read,
    Action<ExprEnvironmentSchemaBuilder<T>> AddToEnvironment);

public sealed class FilterSchema<T>
{
    private readonly Dictionary<string, FilterField<T>> fields;

    public FilterSchema(IEnumerable<FilterField<T>> fields, params string[] examples)
    {
        this.fields = fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        Fields = this.fields.Values.OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
        Examples = examples;

        ExprEnvironmentSchemaBuilder<T> builder = new();
        foreach (FilterField<T> field in Fields)
        {
            field.AddToEnvironment(builder);
        }

        Environment = builder.Build();
    }

    public IReadOnlyList<FilterField<T>> Fields { get; }

    public IReadOnlyList<string> Examples { get; }

    internal ExprEnvironmentSchema Environment { get; }

    public FilterField<T> RequireField(string name) => fields.TryGetValue(name, out FilterField<T>? field)
        ? field
        : throw Kernel.Errors.AppError.Invalid($"invalid filter: unknown field {name}");

    internal bool TryGetField(string name, out FilterField<T>? field) => fields.TryGetValue(name, out field);

    internal static string EnvironmentName(string fieldName) =>
        $"__field_{Convert.ToHexString(Encoding.UTF8.GetBytes(fieldName))}";
}
