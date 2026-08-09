# Continuous integration and native publish matrix

Status: Accepted
Date: 2026-08-09

## Decision

Use GitHub Actions with two deliberately different gates. One Ubuntu job runs
the complete repository loop: pinned tools and SDK, restore, warnings-as-errors
build, formatting, generated-dispatcher freshness, EF model freshness, and the
full test solution. A second matrix publishes and executes the Avalonia
`--help` path as a self-contained native application on Ubuntu x64, Windows
x64, and Intel macOS.

The matrix runs on the operating system it publishes for. This exercises the
native SQLite and Avalonia assets instead of treating a successful cross-RID
archive as proof that the target OS can start it. `macos-15-intel` is selected
explicitly because `macos-latest` is Apple Silicon, while this repository's
baseline publish matrix is x64 across all three systems.

Only official GitHub actions are used. Their immutable release commit SHAs are
pinned, Dependabot-style upgrades remain reviewable, permissions are reduced to
read-only repository contents, and checkout includes the `cedar-dotnet`
submodule recursively.

## Validation

- The exact workflow commands pass locally on the pinned .NET 10 SDK.
- Local self-contained outputs identify as Mach-O x64, ELF x64, and PE32+ x64;
  the native macOS artifact executes its System.CommandLine help path.
- Each hosted runner executes its own native artifact in CI.

## Sources

- [GitHub's .NET build and test guidance](https://docs.github.com/en/actions/tutorials/build-and-test-code/net)
- [GitHub-hosted runner labels and architectures](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)
- [Official runner image matrix](https://github.com/actions/runner-images)
- [Official checkout action releases](https://github.com/actions/checkout/releases)
- [Official setup-dotnet action](https://github.com/actions/setup-dotnet)
