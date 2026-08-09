# Desktop GUI

Status: Accepted  
Date: 2026-08-09

## Decision

Use Avalonia Desktop 12.1.0, Avalonia.Headless.XUnit 12.1.0, and
CommunityToolkit.Mvvm 8.4.2 when the GUI slice begins. These were the current
stable releases on 2026-08-09 and all support the repository's `net10.0`
target. Use XAML compiled bindings, generated observable properties/commands,
and domain-owned MVVM adapters. A repository-owned GUI toolkit supplies shell,
list/detail, forms, dialogs, paging, action projection, UI dispatch, stale-work
guards, and managed shutdown.

Avalonia.Headless.XUnit 12.1.0 is built for xUnit v3. Desktop tests therefore
use xUnit 3.2.2 in their own test project while the pre-existing suites remain
on xUnit v2; the Visual Studio runner supports both. This keeps the official
headless dispatcher integration without forcing an unrelated repository-wide
test-framework migration.

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

## Foundation status

The first slice implements the Generic Host composition root, Dashboard-only
MVVM shell, UI dispatch, latest-request ownership, lazy workspace caching, and
the dirty-navigation confirmation seam. A production confirmation dialog,
logging and metrics option parity, and all seven domain workspaces remain later
slices; the shell deliberately does not advertise them yet.

## Validation

Unit-test view models, test actual XAML/control behavior with
`Avalonia.Headless.XUnit`, capture visual evidence with headless Skia, and run
self-contained packaging smoke tests on Windows, macOS, and Linux.

## Sources

- [Avalonia supported platforms](https://docs.avaloniaui.net/docs/supported-platforms)
- [Avalonia headless testing](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform)
- [Avalonia accessibility](https://docs.avaloniaui.net/docs/app-development/accessibility)
- [Avalonia repository and MIT license](https://github.com/AvaloniaUI/Avalonia)
- [Avalonia.Desktop 12.1.0 package](https://www.nuget.org/packages/Avalonia.Desktop/12.1.0)
- [Avalonia.Headless.XUnit 12.1.0 package](https://www.nuget.org/packages/Avalonia.Headless.XUnit/12.1.0)
- [Avalonia MVVM guidance](https://docs.avaloniaui.net/docs/how-to/mvvm-how-to)
- [CommunityToolkit.Mvvm generators](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/overview)
- [CommunityToolkit.Mvvm 8.4.2 package](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2)
- [xUnit.net v3 3.2.2 package](https://www.nuget.org/packages/xunit.v3/3.2.2)
- [Official MAUI platforms](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0)
- [Experimental MAUI backends](https://learn.microsoft.com/en-us/dotnet/maui/developer-tools/platform-backends/?view=net-maui-10.0)
