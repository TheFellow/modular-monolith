using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(Mixology.Desktop.Tests.TestApplication))]

namespace Mixology.Desktop.Tests;

public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());

    private sealed class HeadlessApplication : Avalonia.Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());
    }
}
