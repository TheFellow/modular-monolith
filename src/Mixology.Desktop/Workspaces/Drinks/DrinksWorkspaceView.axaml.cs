using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mixology.Desktop.Workspaces.Drinks;

public sealed partial class DrinksWorkspaceView : UserControl
{
    public DrinksWorkspaceView() => AvaloniaXamlLoader.Load(this);
}
