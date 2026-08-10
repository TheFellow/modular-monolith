using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mixology.Desktop.Workspaces.Audit;

public sealed partial class AuditView : UserControl
{
    public AuditView() => AvaloniaXamlLoader.Load(this);
}
