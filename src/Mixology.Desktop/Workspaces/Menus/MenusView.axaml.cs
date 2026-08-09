using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mixology.Desktop.Workspaces.Menus;

public sealed partial class MenusView : UserControl
{
    public MenusView() => AvaloniaXamlLoader.Load(this);
}
