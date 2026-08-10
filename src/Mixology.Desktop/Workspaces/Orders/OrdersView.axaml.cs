using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mixology.Desktop.Workspaces.Orders;

public sealed partial class OrdersView : UserControl
{
    public OrdersView() => AvaloniaXamlLoader.Load(this);
}
