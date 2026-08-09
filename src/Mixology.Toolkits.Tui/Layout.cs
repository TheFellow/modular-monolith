namespace Mixology.Toolkits.Tui;

public readonly record struct Viewport
{
    public Viewport(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }

    public Viewport Constrain(int width, int height) => new(
        Math.Min(Width, Math.Max(width, 0)),
        Math.Min(Height, Math.Max(height, 0)));
}

public readonly record struct PaneWidths(int List, int Detail)
{
    public int Total => List + Detail;
}

public readonly record struct Insets(int Left, int Top, int Right, int Bottom)
{
    public int Horizontal => Math.Max(Left, 0) + Math.Max(Right, 0);
    public int Vertical => Math.Max(Top, 0) + Math.Max(Bottom, 0);
}

public static class TuiLayout
{
    public static PaneWidths SplitListDetailWidths(int width)
    {
        if (width <= 0)
        {
            return default;
        }

        int list = (int)(width * 0.6d);
        if (list < 32)
        {
            list = width / 2;
        }

        int detail = width - list;
        if (detail < 24)
        {
            detail = Math.Max(width - 24, 0);
            list = width - detail;
        }

        return new PaneWidths(list, detail);
    }

    public static Viewport ContentViewport(Viewport viewport, Insets insets) => new(
        Math.Max(viewport.Width - insets.Horizontal, 0),
        Math.Max(viewport.Height - insets.Vertical, 0));
}
