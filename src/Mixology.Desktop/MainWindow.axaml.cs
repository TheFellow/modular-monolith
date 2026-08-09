using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mixology.Desktop;

public sealed partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);

    public MainWindow(ShellViewModel shell)
        : this()
    {
        ArgumentNullException.ThrowIfNull(shell);
        DataContext = shell;
    }
}
