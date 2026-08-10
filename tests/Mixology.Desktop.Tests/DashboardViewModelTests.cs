using Mixology.Desktop.Workspaces.Dashboard;
using Mixology.Kernel.Errors;
using Mixology.Presentation.Dashboard;
using Xunit;
using DashboardData = Mixology.Presentation.Dashboard.Dashboard;

namespace Mixology.Desktop.Tests;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task SupersededResponseCannotReplaceLatestDashboard()
    {
        TaskCompletionSource<DashboardResult> first = Source<DashboardResult>();
        TaskCompletionSource<DashboardResult> second = Source<DashboardResult>();
        Queue<TaskCompletionSource<DashboardResult>> loads = new([first, second]);
        await using DashboardViewModel viewModel = new(_ => loads.Dequeue().Task);

        Task stale = viewModel.RefreshAsync();
        Task current = viewModel.RefreshAsync();
        second.SetResult(Result(22));
        await current;
        first.SetResult(Result(11));
        await stale;

        Assert.Equal("22", viewModel.DrinkCount);
        Assert.False(viewModel.IsRefreshing);
    }

    [Fact]
    public async Task TypedErrorsKeepIdentityAndUnknownErrorsBecomeSafeInternalErrors()
    {
        InvalidError typed = AppError.Invalid("bad dashboard filter");
        await using DashboardViewModel typedViewModel = new(_ => Task.FromException<DashboardResult>(typed));
        await typedViewModel.RefreshAsync();
        Assert.Same(typed, typedViewModel.Error);
        Assert.Equal("bad dashboard filter", typedViewModel.StatusMessage);

        InvalidOperationException cause = new("database secret");
        await using DashboardViewModel unknownViewModel = new(_ => Task.FromException<DashboardResult>(cause));
        await unknownViewModel.RefreshAsync();
        InternalError normalized = Assert.IsType<InternalError>(unknownViewModel.Error);
        Assert.Same(cause, normalized.InnerException);
        Assert.Equal("internal error", unknownViewModel.StatusMessage);
    }

    [Fact]
    public async Task CancellationIsNeverConvertedIntoAnApplicationError()
    {
        await using DashboardViewModel viewModel = new(async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Result(0);
        });
        using CancellationTokenSource cancellation = new();

        Task refresh = viewModel.RefreshAsync(cancellation.Token);
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
        Assert.Null(viewModel.Error);
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
        8,
        [new DashboardActivity(
            DateTimeOffset.Parse("2026-08-09T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            "owner",
            "created drink")]));

    private static TaskCompletionSource<T> Source<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
