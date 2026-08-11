using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace Mixology.Gui;

public sealed partial class DesktopApplication : Microsoft.Maui.Controls.Application
{
    private readonly DesktopOptions options;
    private DesktopSession? session;

    public DesktopApplication(DesktopOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        session = DesktopSession.Open(options);
        Window window = new(new MainPage(session.Shell))
        {
            Title = "Mixology",
            Width = 1080,
            Height = 720,
            MinimumWidth = 720,
            MinimumHeight = 480,
        };
        window.Destroying += OnDestroying;
        return window;
    }

    private void OnDestroying(object? sender, EventArgs args)
    {
        if (sender is Window window)
        {
            window.Destroying -= OnDestroying;
        }

        if (session is not null)
        {
            session.DisposeAsync().AsTask().GetAwaiter().GetResult();
            session = null;
        }
    }
}
