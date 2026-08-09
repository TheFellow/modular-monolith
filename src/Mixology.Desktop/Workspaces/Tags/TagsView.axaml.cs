using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mixology.Desktop.Workspaces.Tags;

public sealed partial class TagsView : UserControl
{
    public TagsView() => AvaloniaXamlLoader.Load(this);
}
