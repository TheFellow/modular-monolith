using Microsoft.Extensions.Hosting;
using Mixology.Application.Authentication;
using Mixology.Application.Operations;
using Mixology.Persistence;

namespace Mixology.Application;

public sealed class MixologySession
{
    private readonly OperationPipeline pipeline;
    private readonly CancellationToken applicationCancellation;

    internal MixologySession(
        OperationPipeline pipeline,
        Actor actor,
        CancellationToken applicationCancellation)
    {
        this.pipeline = pipeline;
        this.applicationCancellation = applicationCancellation;
        Actor = actor.IsEmpty ? Actor.Anonymous : actor;
    }

    public Actor Actor { get; }

    public Task ExecuteAsync(
        Operation operation,
        OperationDelegate execute,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(operation, execute, null, cancellationToken);

    public async Task<TResult> ExecuteAsync<TResult>(
        Operation operation,
        Func<OperationContext, Task<TResult>> execute,
        CancellationToken cancellationToken = default)
    {
        TResult? result = default;
        await ExecuteCoreAsync(operation, async context =>
        {
            result = await execute(context).ConfigureAwait(false);
        }, null, cancellationToken).ConfigureAwait(false);
        return result!;
    }

    public Task ExecuteInSessionAsync(
        StoreSession storeSession,
        Operation operation,
        OperationDelegate execute,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(operation, execute, storeSession, cancellationToken);

    private async Task ExecuteCoreAsync(
        Operation operation,
        OperationDelegate execute,
        StoreSession? storeSession,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource? linked = LinkCancellation(cancellationToken);
        CancellationToken effective = linked?.Token
            ?? (cancellationToken.CanBeCanceled ? cancellationToken : applicationCancellation);
        await pipeline.ExecuteAsync(
            new OperationContext(Actor, storeSession, effective),
            operation,
            execute).ConfigureAwait(false);
    }

    private CancellationTokenSource? LinkCancellation(CancellationToken cancellationToken) =>
        applicationCancellation.CanBeCanceled && cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(applicationCancellation, cancellationToken)
            : null;
}

public sealed class MixologySessionFactory(
    OperationPipeline pipeline,
    IHostApplicationLifetime? lifetime = null)
{
    public MixologySession Create(Actor actor) =>
        new(pipeline, actor, lifetime?.ApplicationStopping ?? default);
}
