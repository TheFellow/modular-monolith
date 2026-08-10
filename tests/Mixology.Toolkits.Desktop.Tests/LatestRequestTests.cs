using Mixology.Toolkits.Desktop.Threading;
using Xunit;

namespace Mixology.Toolkits.Desktop.Tests;

public sealed class LatestRequestTests
{
    [Fact]
    public async Task LateResponseCannotBecomeCurrentWhenOperationIgnoresCancellation()
    {
        TaskCompletionSource<int> first = Source<int>();
        TaskCompletionSource<int> second = Source<int>();
        await using LatestRequest<int> requests = new();

        Task<LatestResult<int>> stale = requests.RunAsync(
            _ => first.Task,
            TestContext.Current.CancellationToken);
        Task<LatestResult<int>> current = requests.RunAsync(
            _ => second.Task,
            TestContext.Current.CancellationToken);
        second.SetResult(22);
        first.SetResult(11);

        LatestResult<int> currentResult = await current;
        LatestResult<int> staleResult = await stale;
        Assert.True(currentResult.IsCurrent);
        Assert.Equal(22, currentResult.Value);
        Assert.False(staleResult.IsCurrent);
    }

    [Fact]
    public async Task NewGenerationCancelsPreviousRequest()
    {
        TaskCompletionSource cancelled = Source();
        await using LatestRequest<int> requests = new();
        Task<LatestResult<int>> stale = requests.RunAsync(
            async token =>
            {
                using CancellationTokenRegistration registration = token.Register(cancelled.SetResult);
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return 0;
            },
            TestContext.Current.CancellationToken);

        Task<LatestResult<int>> current = requests.RunAsync(
            _ => Task.FromResult(2),
            TestContext.Current.CancellationToken);

        await cancelled.Task;
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stale);
        Assert.True((await current).IsCurrent);
    }

    [Fact]
    public async Task DisposalCancelsDrainsAndObservesAcceptedWork()
    {
        TaskCompletionSource started = Source();
        TaskCompletionSource stopped = Source();
        LatestRequest<int> requests = new();
        Task<LatestResult<int>> pending = requests.RunAsync(
            async token =>
            {
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                finally
                {
                    stopped.SetResult();
                }

                return 0;
            },
            TestContext.Current.CancellationToken);
        await started.Task;

        await requests.DisposeAsync();

        await stopped.Task;
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            requests.RunAsync(
                _ => Task.FromResult(1),
                TestContext.Current.CancellationToken));
    }

    private static TaskCompletionSource Source() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource<T> Source<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
