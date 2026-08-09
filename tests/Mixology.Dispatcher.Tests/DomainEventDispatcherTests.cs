using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Application.Operations;

namespace Mixology.Dispatcher.Tests;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task UnknownEventsAreValidExtensionPoints()
    {
        ServiceCollection services = new();
        using ServiceProvider provider = services.BuildServiceProvider();
        DomainEventDispatcher dispatcher = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DomainEventDispatcher>.Instance);
        DispatchEventsMiddleware middleware = new(dispatcher);
        OperationContext context = new(Actor.Owner);

        await middleware.InvokeAsync(
            context,
            Operation.Command("test:unknown-event"),
            operationContext =>
            {
                operationContext.AddEvent(new UnknownEvent());
                return Task.CompletedTask;
            });
    }

    private sealed record UnknownEvent;
}
