using Mixology.Application.Authentication;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
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

        await chain.ExecuteAsync(new OperationContext(Actor.Owner), Operation.Command("create"), _ =>
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
        OperationContext baseContext = new(Actor.Owner);
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

    [Fact]
    public async Task TouchesAreDeduplicatedWithoutLosingInsertionOrder()
    {
        EntityUid first = new("Mixology::Ingredient", "ing-first");
        EntityUid second = new("Mixology::Drink", "drk-second");
        OperationContext? observed = null;
        OperationChain chain = new([]);

        await chain.ExecuteAsync(new OperationContext(default), Operation.Command("test"), context =>
        {
            observed = context;
            context.Touch(first);
            context.Touch(second);
            context.Touch(first);
            return Task.CompletedTask;
        });

        Assert.NotNull(observed);
        Assert.Equal(Actor.Anonymous, observed.Principal);
        Assert.Equal([first, second], observed.TouchedEntities);
    }
}
