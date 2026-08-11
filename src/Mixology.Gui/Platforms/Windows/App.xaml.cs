using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace Mixology.Gui.WinUI;

public sealed partial class App : MauiWinUIApplication
{
    public App() => InitializeComponent();

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
