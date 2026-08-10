using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mixology.Desktop.Workspaces.Dashboard;

public sealed partial class DashboardView : UserControl
{
    public DashboardView() => AvaloniaXamlLoader.Load(this);
}
