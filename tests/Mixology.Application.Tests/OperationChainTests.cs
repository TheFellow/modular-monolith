using Mixology.Application.Operations;
using Xunit;

namespace Mixology.Application.Tests;

public sealed class OperationChainTests
{
    [Fact]
    public async Task MiddlewareWrapsInDeclaredOrder()
    {
        List<string> calls = [];
        OperationMiddleware first = async (context, operation, next) =>
        {
            calls.Add($"first-before-{operation.Action}-{context.Principal}");
            await next(context);
            calls.Add("first-after");
        };
        OperationMiddleware second = async (context, _, next) =>
        {
            calls.Add("second-before");
            await next(context);
            calls.Add("second-after");
        };
        OperationChain chain = new([first, second]);

        await chain.ExecuteAsync(new OperationContext("owner"), Operation.Command("create"), _ =>
        {
            calls.Add("handler");
            return Task.CompletedTask;
        });

        Assert.Equal(
            ["first-before-create-owner", "second-before", "handler", "second-after", "first-after"],
            calls);
    }

    [Fact]
    public async Task EveryExecutionGetsFreshMutableState()
    {
        OperationContext baseContext = new("owner");
        OperationChain chain = new([]);
        List<int> initialEventCounts = [];

        for (int index = 0; index < 2; index++)
        {
            await chain.ExecuteAsync(baseContext, Operation.Command("test"), context =>
            {
                initialEventCounts.Add(context.Events.Count);
                context.AddEvent(new object());
                return Task.CompletedTask;
            });
        }

        Assert.Equal([0, 0], initialEventCounts);
        Assert.Empty(baseContext.Events);
    }
}
