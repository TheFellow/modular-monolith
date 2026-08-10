using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Mixology.Desktop;

public sealed partial class DesktopApplication : Avalonia.Application
{
    private readonly ShellViewModel? shell;

    public DesktopApplication()
    {
    }

    public DesktopApplication(ShellViewModel shell) =>
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(shell
                ?? throw new InvalidOperationException("The desktop shell was not configured."));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
