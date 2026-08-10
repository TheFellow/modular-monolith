using Terminal.Gui.Input;
using Terminal.Gui.Views;
using Xunit;

namespace Mixology.Toolkits.Tui.Tests;

public sealed class TerminalApplicationHostTests
{
    [Fact]
    public void EachHostOwnsAndDisposesItsApplicationAndDriver()
    {
        TerminalApplicationHost first = TerminalApplicationHost.CreateAnsi();
        object firstApplication = first.Application;
        first.Initialize();
        object? firstDriver = first.Application.Driver;

        Assert.Equal(TerminalApplicationState.Initialized, first.State);
        Assert.NotNull(firstDriver);
        Assert.Throws<TuiLifecycleException>(first.Initialize);
        first.Dispose();
        Assert.Equal(TerminalApplicationState.Disposed, first.State);
        Assert.Throws<ObjectDisposedException>(() => first.Application);

        using TerminalApplicationHost second = TerminalApplicationHost.CreateAnsi();
        Assert.NotSame(firstApplication, second.Application);
        second.Initialize();
        Assert.NotSame(firstDriver, second.Application.Driver);
    }

    [Fact]
    public async Task PreCancelledRunDoesNotInitializeOrConsumeTheRoot()
    {
        using TerminalApplicationHost host = TerminalApplicationHost.CreateAnsi();
        using Window root = new() { Title = "Never mounted" };
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await host.RunAsync(root, cancellation.Token));

        Assert.Equal(TerminalApplicationState.Created, host.State);
        Assert.False(root.IsRunning);
    }

    [Fact]
    public void InputOwnershipRoutesLocalBeforeGlobalAndSuppressesCapturedKeys()
    {
        List<string> routed = [];
        Key editKey = Key.A;

        bool handled = InputRouter.Dispatch(
            editKey,
            InputOwnership.Edit,
            key =>
            {
                routed.Add($"local:{key}");
                return false;
            },
            key =>
            {
                routed.Add($"global:{key}");
                return true;
            });

        Assert.True(handled);
        Assert.True(editKey.Handled);
        Assert.Single(routed);

        routed.Clear();
        Key browseKey = Key.R;
        handled = InputRouter.Dispatch(
            browseKey,
            InputOwnership.Browse,
            _ =>
            {
                routed.Add("local");
                return false;
            },
            _ =>
            {
                routed.Add("global");
                return true;
            });

        Assert.True(handled);
        Assert.Equal(["local", "global"], routed);
    }

    [Fact]
    public void BackOwnedByNestedViewNeverLeaksToGlobalNavigation()
    {
        int local = 0;
        int global = 0;

        bool handled = InputRouter.Dispatch(
            Key.Esc,
            new InputOwnership(false, true),
            _ =>
            {
                local++;
                return false;
            },
            _ =>
            {
                global++;
                return true;
            });

        Assert.True(handled);
        Assert.Equal(1, local);
        Assert.Equal(0, global);
    }
}
