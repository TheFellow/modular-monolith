using Xunit;

namespace Mixology.Toolkits.Tui.Tests;

public sealed class LayoutAndListItemTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100, 60, 40)]
    [InlineData(80, 48, 32)]
    [InlineData(50, 25, 25)]
    [InlineData(40, 24, 16)]
    [InlineData(20, 20, 0)]
    public void ListDetailSplitPreservesReferenceBounds(int width, int list, int detail)
    {
        PaneWidths split = TuiLayout.SplitListDetailWidths(width);

        Assert.Equal(list, split.List);
        Assert.Equal(detail, split.Detail);
        Assert.Equal(Math.Max(width, 0), split.Total);
    }

    [Fact]
    public void ContentViewportNeverEscapesTerminalBounds()
    {
        Viewport content = TuiLayout.ContentViewport(
            new Viewport(80, 24),
            new Insets(2, 1, 2, 1));

        Assert.Equal(new Viewport(76, 22), content);
        Assert.Equal(new Viewport(40, 10), content.Constrain(40, 10));
        Assert.Equal(new Viewport(0, 0), TuiLayout.ContentViewport(
            new Viewport(2, 1),
            new Insets(4, 4, 4, 4)));
    }

    [Fact]
    public void TypedListItemPreservesPresentationAndDefaultsFilterToTitle()
    {
        ListItem<(int Id, string Name)> item = new(
            (42, "answer"),
            "Title",
            "Description");

        Assert.Equal((42, "answer"), item.Value);
        Assert.Equal("Title", item.Title);
        Assert.Equal("Description", item.Description);
        Assert.Equal("Title", item.FilterValue);
    }
}
