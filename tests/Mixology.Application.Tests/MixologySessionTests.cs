using Microsoft.Extensions.DependencyInjection;
using Mixology.Application.Authentication;
using Mixology.Application.Operations;
using Xunit;
using StoreFixture = Mixology.Application.Tests.UnitOfWorkMiddlewareTests.StoreFixture;

namespace Mixology.Application.Tests;

public sealed class MixologySessionTests
{
    [Fact]
    public async Task SessionBindsActorButCreatesFreshOperationState()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        MixologySession session = fixture.Services
            .GetRequiredService<MixologySessionFactory>()
            .Create(Actor.Manager);
        List<OperationContext> contexts = [];

        for (int index = 0; index < 2; index++)
        {
            await session.ExecuteAsync(Operation.Query("probe.list"), context =>
            {
                contexts.Add(context);
                Assert.Equal(Actor.Manager, context.Principal);
                Assert.Empty(context.Events);
                context.AddEvent(new object());
                return Task.CompletedTask;
            });
        }

        Assert.Equal(2, contexts.Count);
        Assert.NotSame(contexts[0], contexts[1]);
    }

    [Fact]
    public async Task CallerCancellationFlowsIntoTheOperation()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        MixologySession session = fixture.Services
            .GetRequiredService<MixologySessionFactory>()
            .Create(Actor.Bartender);
        using CancellationTokenSource caller = new();
        caller.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ExecuteAsync(
            Operation.Query("probe.list"),
            context => Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken),
            caller.Token));
    }
}
