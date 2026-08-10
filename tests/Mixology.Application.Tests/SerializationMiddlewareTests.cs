using Mixology.Application.Authentication;
using Mixology.Application.Operations;
using Xunit;
using StoreFixture = Mixology.Application.Tests.UnitOfWorkMiddlewareTests.StoreFixture;

namespace Mixology.Application.Tests;

public sealed class SerializationMiddlewareTests
{
    [Fact]
    public async Task SharedSessionSerializesTheCompleteOperation()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        await using Mixology.Persistence.StoreSession session = await fixture.Store.OpenSessionAsync();
        SerializationMiddleware middleware = new();
        int active = 0;
        int maximum = 0;

        Task[] operations = Enumerable.Range(0, 6).Select(_ => middleware.InvokeAsync(
            new OperationContext(Actor.Owner, session),
            Operation.Query("probe.list"),
            async _ =>
            {
                int current = Interlocked.Increment(ref active);
                int observed;
                do
                {
                    observed = maximum;
                }
                while (current > observed && Interlocked.CompareExchange(ref maximum, current, observed) != observed);

                await Task.Delay(10);
                Interlocked.Decrement(ref active);
            })).ToArray();

        await Task.WhenAll(operations);

        Assert.Equal(1, maximum);
    }
}
