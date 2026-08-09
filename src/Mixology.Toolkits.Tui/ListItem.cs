namespace Mixology.Toolkits.Tui;

public sealed record ListItem<T>
{
    public ListItem(T value, string title, string description, string? filterValue = null)
    {
        Value = value;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        FilterValue = string.IsNullOrEmpty(filterValue) ? title : filterValue;
    }

    public T Value { get; }
    public string Title { get; }
    public string Description { get; }
    public string FilterValue { get; }
}
