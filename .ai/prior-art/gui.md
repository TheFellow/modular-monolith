# Desktop GUI

Status: Accepted  
Date: 2026-08-09

## Decision

Use Avalonia Desktop 12.1.x with CommunityToolkit.Mvvm 8.4.x when the GUI slice
begins. Use XAML compiled bindings, generated observable properties/commands,
and domain-owned MVVM adapters. A repository-owned GUI toolkit supplies shell,
list/detail, forms, dialogs, paging, action projection, UI dispatch, stale-work
guards, and managed shutdown.

MAUI is not selected. The Go desktop application supports Windows, macOS, and
Linux. MAUI's supported production desktop targets are Windows and Mac Catalyst;
its GTK Linux backend remains experimental and community-supported. Avalonia
supports all three directly and has a first-class headless platform for real
controls, layout, bindings, keyboard/mouse input, dispatcher flushing, and
rendered frames.

View models are native to retained desktop interaction and are not shared with
the TUI. They preserve dirty-editor confirmation, request-generation ownership,
latest-result-wins publication, duplicate-submission gates, authorization-shaped
navigation/actions, and draining accepted work before closing storage.

## Validation

Unit-test view models, test actual XAML/control behavior with
`Avalonia.Headless.XUnit`, capture visual evidence with headless Skia, and run
self-contained packaging smoke tests on Windows, macOS, and Linux.

## Sources

- [Avalonia supported platforms](https://docs.avaloniaui.net/docs/supported-platforms)
- [Avalonia headless testing](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform)
- [Avalonia accessibility](https://docs.avaloniaui.net/docs/app-development/accessibility)
- [Avalonia repository and MIT license](https://github.com/AvaloniaUI/Avalonia)
- [Avalonia MVVM guidance](https://docs.avaloniaui.net/docs/how-to/mvvm-how-to)
- [CommunityToolkit.Mvvm generators](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/overview)
- [Official MAUI platforms](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0)
- [Experimental MAUI backends](https://learn.microsoft.com/en-us/dotnet/maui/developer-tools/platform-backends/?view=net-maui-10.0)

