using Mixology.Kernel.Errors;
using Mixology.Presentation.Dashboard;
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces;
using Xunit;
using DashboardData = Mixology.Presentation.Dashboard.Dashboard;

namespace Mixology.Tui.Tests;

public sealed class DashboardWorkspaceTests
{
    [Fact]
    public async Task ContentViewportAtValidTerminalMinimumRendersDashboard()
    {
        await using DashboardWorkspace workspace = new(_ => Task.FromResult(Result(3)));

        await workspace.ActivateAsync();
        string rendered = workspace.Render(new Viewport(80, 21));

        Assert.Contains("Dashboard", rendered, StringComparison.Ordinal);
        Assert.Contains("Drinks", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Terminal too small", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("[0]", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("[1]", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupersededRefreshCannotReplaceTheNewerResult()
    {
        TaskCompletionSource<DashboardResult> first = Source();
        TaskCompletionSource<DashboardResult> second = Source();
        Queue<TaskCompletionSource<DashboardResult>> loads = new([first, second]);
        await using DashboardWorkspace workspace = new(_ => loads.Dequeue().Task);

        Task old = workspace.RefreshAsync();
        Task current = workspace.RefreshAsync();
        second.SetResult(Result(22));
        await current;
        first.SetResult(Result(11));
        await old;

        string rendered = workspace.Render(new Viewport(80, 21));
        Assert.Contains("22", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("  11", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisposalCancelsAndObservesEverySupersededRequest()
    {
        int cancelled = 0;
        int started = 0;
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DashboardWorkspace workspace = new(async cancellationToken =>
        {
            int request = Interlocked.Increment(ref started);
            (request == 1 ? firstStarted : secondStarted).SetResult();
            TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                _ = Interlocked.Increment(ref cancelled);
                _ = stopped.TrySetCanceled(cancellationToken);
            });
            await stopped.Task;
            return Result(0);
        });

        Task first = workspace.RefreshAsync();
        await firstStarted.Task;
        Task second = workspace.RefreshAsync();
        await secondStarted.Task;
        await workspace.DisposeAsync();

        Assert.Equal(2, Volatile.Read(ref cancelled));
        Assert.True(first.IsCanceled);
        Assert.True(second.IsCanceled);
    }

    [Fact]
    public async Task PartialTypedFailureKeepsDataAndPublishesTerminalStatus()
    {
        DashboardResult partial = new(Result(7).Data, AppError.Conflict("dashboard temporarily unavailable"));
        await using DashboardWorkspace workspace = new(_ => Task.FromResult(partial));

        await workspace.ActivateAsync();

        Assert.Equal("dashboard temporarily unavailable", workspace.Status?.Message);
        Assert.Equal(TerminalErrorStyle.Warning, workspace.Status?.Style);
        Assert.Contains("7", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);
    }

    private static DashboardResult Result(int drinks) => new(new DashboardData(
        drinks,
        2,
        3,
        4,
        1,
        3,
        1,
        5,
        2,
        6,
        [new DashboardActivity(
            new DateTimeOffset(2026, 8, 9, 12, 30, 0, TimeSpan.Zero),
            "Mixology::Actor::\"owner\"",
            "Mixology::Drink::Action::\"create\"")]));

    private static TaskCompletionSource<DashboardResult> Source() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
