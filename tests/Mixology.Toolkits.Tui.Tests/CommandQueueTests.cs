using Xunit;

namespace Mixology.Toolkits.Tui.Tests;

public sealed class CommandQueueTests
{
    [Fact]
    public async Task DrainIsFifoIncludingWorkEnqueuedByCommands()
    {
        TuiCommandQueue queue = new();
        List<string> order = [];
        queue.Enqueue((context, _) =>
        {
            order.Add("first");
            context.Enqueue((_, _) =>
            {
                order.Add("nested");
                return ValueTask.CompletedTask;
            });
            return ValueTask.CompletedTask;
        });
        queue.Enqueue((_, _) =>
        {
            order.Add("second");
            return ValueTask.CompletedTask;
        });

        int drained = await queue.DrainAsync();

        Assert.Equal(3, drained);
        Assert.Equal(["first", "second", "nested"], order);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public async Task DrainPreservesCancellationAndRemainingWork()
    {
        TuiCommandQueue queue = new();
        int executed = 0;
        queue.Enqueue((_, _) =>
        {
            executed++;
            return ValueTask.CompletedTask;
        });
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await queue.DrainAsync(cancellationToken: cancellation.Token));

        Assert.Equal(0, executed);
        Assert.Equal(1, queue.PendingCount);
        Assert.Equal(1, await queue.DrainAsync());
    }

    [Fact]
    public async Task RunawayWorkFailsWithTypedDrainLimitAndCanResume()
    {
        TuiCommandQueue queue = new();
        TuiCommand? repeat = null;
        repeat = (context, _) =>
        {
            context.Enqueue(repeat!);
            return ValueTask.CompletedTask;
        };
        queue.Enqueue(repeat);

        CommandDrainLimitExceededException failure = await Assert.ThrowsAsync<CommandDrainLimitExceededException>(
            async () => await queue.DrainAsync(limit: 3));

        Assert.Equal(3, failure.Limit);
        Assert.Equal(1, queue.PendingCount);
    }
}
