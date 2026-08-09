using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Errors;
using Xunit;

namespace Mixology.Application.Tests;

public sealed class DispatchEventsMiddlewareTests
{
    [Fact]
    public async Task CommandDispatchesAfterBodyInEmissionOrder()
    {
        RecordingDispatcher dispatcher = new();
        DispatchEventsMiddleware middleware = new(dispatcher);
        OperationContext context = new(Actor.Owner);

        await middleware.InvokeAsync(context, Operation.Command("test"), operationContext =>
        {
            dispatcher.Calls.Add("body");
            operationContext.AddEvent("first");
            operationContext.AddEvent("second");
            return Task.CompletedTask;
        });

        Assert.Equal(["body", "first", "second"], dispatcher.Calls);
        Assert.DoesNotContain(
            typeof(EventHandlerContext).GetMethods(),
            method => method.Name == nameof(OperationContext.AddEvent));
    }

    [Fact]
    public async Task QueriesDoNotDispatch()
    {
        RecordingDispatcher dispatcher = new();
        DispatchEventsMiddleware middleware = new(dispatcher);
        OperationContext context = new(Actor.Anonymous);

        await middleware.InvokeAsync(context, Operation.Query("test"), operationContext =>
        {
            operationContext.AddEvent("ignored");
            return Task.CompletedTask;
        });

        Assert.Empty(dispatcher.Calls);
    }

    [Fact]
    public async Task DispatcherFailuresBecomeTypedInternalErrors()
    {
        DispatchEventsMiddleware middleware = new(new FailingDispatcher());
        OperationContext context = new(Actor.Owner);

        InternalError error = await Assert.ThrowsAsync<InternalError>(() => middleware.InvokeAsync(
            context,
            Operation.Command("test"),
            operationContext =>
            {
                operationContext.AddEvent("event");
                return Task.CompletedTask;
            }));

        Assert.IsType<IOException>(error.InnerException);
    }

    private sealed class RecordingDispatcher : IDomainEventDispatcher
    {
        public List<string> Calls { get; } = [];

        public Task DispatchAsync(EventHandlerContext context, object domainEvent)
        {
            _ = context;
            Calls.Add((string)domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(EventHandlerContext context, object domainEvent)
        {
            _ = context;
            _ = domainEvent;
            throw new IOException("dispatch failed");
        }
    }
}
