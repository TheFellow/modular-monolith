using Microsoft.Maui.Controls;

namespace Mixology.Desktop;

public sealed partial class MainPage : ContentPage
{
    public MainPage(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        InitializeComponent();
        BindingContext = shell;
    }
}
