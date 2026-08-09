using Terminal.Gui.Views;
using Xunit;

namespace Mixology.Toolkits.Tui.Tests;

public sealed class TableModelTests
{
    [Fact]
    public void NativeTableSourceAndSelectionRemainStableAcrossRefresh()
    {
        TableModel<Row, int> table = new(
            static row => row.Id,
            [
                new TableColumn<Row>("Name", static row => row.Name),
                new TableColumn<Row>("Count", static row => row.Count),
            ],
            [new Row(1, "one", 10), new Row(2, "two", 20), new Row(3, "three", 30)]);
        table.Select(1);

        ITableSource source = table.Source;
        Assert.Equal(3, source.Rows);
        Assert.Equal(2, source.Columns);
        Assert.Equal(["Name", "Count"], source.ColumnNames);
        Assert.Equal("two", source[1, 0]);
        Assert.Equal(20, source[1, 1]);

        table.Replace([new Row(3, "three", 31), new Row(2, "two", 21)]);

        Assert.Equal(1, table.SelectedIndex);
        Assert.True(table.TryGetSelected(out Row? selected));
        Assert.NotNull(selected);
        Assert.Equal(2, selected.Id);
        Assert.Equal(21, selected.Count);
    }

    [Fact]
    public void RemovingSelectionChoosesNearestRowAndEmptyTableHasNoSelection()
    {
        TableModel<Row, int> table = Create([new Row(1, "one", 1), new Row(2, "two", 2)]);
        table.Select(1);

        table.Replace([new Row(1, "one", 1)]);
        Assert.Equal(0, table.SelectedIndex);
        Assert.True(table.TryGetSelected(out _));

        table.Replace([]);
        Assert.Equal(-1, table.SelectedIndex);
        Assert.False(table.TryGetSelected(out _));
    }

    [Fact]
    public void InitialSelectionUsesFirstRowEvenWhenLaterValueKeyIsDefault()
    {
        TableModel<Row, int> table = Create([new Row(2, "first", 1), new Row(0, "second", 2)]);

        Assert.Equal(0, table.SelectedIndex);
        Assert.True(table.TryGetSelected(out Row? selected));
        Assert.NotNull(selected);
        Assert.Equal(2, selected.Id);
    }

    [Fact]
    public void DuplicateStableKeysAreRejected()
    {
        TableModel<Row, int> table = Create([]);

        ArgumentException failure = Assert.Throws<ArgumentException>(() =>
            table.Replace([new Row(1, "one", 1), new Row(1, "duplicate", 2)]));

        Assert.Contains("keys", failure.Message, StringComparison.Ordinal);
    }

    private static TableModel<Row, int> Create(IEnumerable<Row> rows) => new(
        static row => row.Id,
        [new TableColumn<Row>("Name", static row => row.Name)],
        rows);

    private sealed record Row(int Id, string Name, int Count);
}
