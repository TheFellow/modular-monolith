# Desktop GUI

Status: Accepted (supersedes the 2026-08-09 Avalonia decision)
Date: 2026-08-10

## Decision

Use .NET MAUI from the pinned .NET 10 SDK for the desktop GUI. Support Windows
through WinUI 3 and macOS through Mac Catalyst. Do not produce a Linux GUI.
Keeping the desktop implementation in Microsoft's .NET SDK, workloads, XAML
compiler, platform hosts, dependency injection, and application lifecycle is
more important for this port than matching the Go reference's Linux surface.

Keep a UI-neutral `net10.0` target in `Mixology.Desktop`. Linux and contributors
without a MAUI workload can build and test option parsing, Generic Host
composition, authorization-filtered navigation, workspaces, and view models.
Windows and macOS add only their native target framework on the matching host.
This avoids requiring Apple or Windows platform packs for the ordinary Linux CI
gate while keeping one project and one source tree.

Views remain desktop-owned and use MAUI XAML compiled bindings. View models are
not shared with the TUI. `DesktopSession` owns Generic Host and shell lifetime;
the MAUI window destroys that session. Native adapters own UI dispatch and dirty
navigation alerts. CommunityToolkit.Mvvm remains the source-generator layer for
observable state and commands, not a UI abstraction.

## Consequences

- Linux retains CLI, TUI, all domain behavior, desktop view-model tests, and XAML
  contract tests, but no desktop window or package.
- Native control execution and packaging must run on Windows and macOS with the
  MAUI workload installed.
- Avalonia packages, application lifetime, controls, dispatcher, modal, and
  headless test infrastructure are removed.
- Platform-neutral tests validate view-model behavior and XAML structure. Native
  CI compiles and publishes both supported GUI targets.

## Sources

- [.NET MAUI overview and native platform architecture](https://learn.microsoft.com/en-us/dotnet/maui/what-is-maui?view=net-maui-10.0)
- [.NET MAUI supported platforms](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0)
- [.NET MAUI application lifecycle](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/app-lifecycle?view=net-maui-10.0)
- [.NET MAUI compiled bindings](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/compiled-bindings?view=net-maui-10.0)
- [.NET MAUI dependency injection](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/dependency-injection?view=net-maui-10.0)
- [.NET MAUI workload installation](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation?view=net-maui-10.0)
