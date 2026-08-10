using Terminal.Gui.Views;

namespace Mixology.Toolkits.Tui;

public sealed record TableColumn<T>
{
    public TableColumn(string header, Func<T, object?> value)
    {
        Header = string.IsNullOrWhiteSpace(header)
            ? throw new ArgumentException("table column header is required", nameof(header))
            : header.Trim();
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string Header { get; }
    public Func<T, object?> Value { get; }
}

public sealed class TableModel<T, TKey>
    where TKey : notnull
{
    private readonly Func<T, TKey> key;
    private readonly TableColumn<T>[] columns;
    private T[] rows = [];

    public TableModel(
        Func<T, TKey> key,
        IEnumerable<TableColumn<T>> columns,
        IEnumerable<T>? rows = null)
    {
        this.key = key ?? throw new ArgumentNullException(nameof(key));
        ArgumentNullException.ThrowIfNull(columns);
        this.columns = columns.ToArray();
        if (this.columns.Length == 0)
        {
            throw new ArgumentException("table must contain at least one column", nameof(columns));
        }

        if (this.columns.Select(static column => column.Header).Distinct(StringComparer.Ordinal).Count()
            != this.columns.Length)
        {
            throw new ArgumentException("table column headers must be unique", nameof(columns));
        }

        Replace(rows ?? []);
    }

    public IReadOnlyList<T> Rows => rows;
    public int SelectedIndex { get; private set; } = -1;

    public ITableSource Source
    {
        get
        {
            Dictionary<string, Func<T, object>> definitions = columns.ToDictionary(
                static column => column.Header,
                static column => new Func<T, object>(row => column.Value(row) ?? string.Empty),
                StringComparer.Ordinal);
            return new EnumerableTableSource<T>(rows, definitions);
        }
    }

    public bool TryGetSelected(out T? selected)
    {
        if (SelectedIndex < 0)
        {
            selected = default;
            return false;
        }

        selected = rows[SelectedIndex];
        return true;
    }

    public void Select(int index)
    {
        if ((uint)index >= (uint)rows.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        SelectedIndex = index;
    }

    public void Replace(IEnumerable<T> nextRows)
    {
        ArgumentNullException.ThrowIfNull(nextRows);
        bool hadSelection = SelectedIndex >= 0;
        TKey? selectedKey = hadSelection ? key(rows[SelectedIndex]) : default;
        int priorIndex = SelectedIndex;
        T[] next = nextRows.ToArray();
        if (next.Select(key).Distinct().Count() != next.Length)
        {
            throw new ArgumentException("table row keys must be unique", nameof(nextRows));
        }

        rows = next;
        if (rows.Length == 0)
        {
            SelectedIndex = -1;
            return;
        }

        if (hadSelection)
        {
            int restored = Array.FindIndex(rows, row => EqualityComparer<TKey>.Default.Equals(key(row), selectedKey));
            if (restored >= 0)
            {
                SelectedIndex = restored;
                return;
            }
        }

        SelectedIndex = priorIndex < 0 ? 0 : Math.Min(priorIndex, rows.Length - 1);
    }
}
