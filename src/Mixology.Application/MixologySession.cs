using Microsoft.Extensions.Hosting;
using Mixology.Application.Authentication;
using Mixology.Application.Operations;
using Mixology.Persistence;

namespace Mixology.Application;

public sealed class MixologySession
{
    private readonly OperationPipeline pipeline;
    private readonly MixologyStore store;
    private readonly CancellationToken applicationCancellation;
    private readonly StoreSession? boundStoreSession;

    internal MixologySession(
        OperationPipeline pipeline,
        MixologyStore store,
        Actor actor,
        CancellationToken applicationCancellation,
        StoreSession? boundStoreSession = null)
    {
        this.pipeline = pipeline;
        this.store = store;
        this.applicationCancellation = applicationCancellation;
        this.boundStoreSession = boundStoreSession;
        Actor = actor.IsEmpty ? Actor.Anonymous : actor;
    }

    public Actor Actor { get; }

    public Task ExecuteAsync(
        Operation operation,
        OperationDelegate execute,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(operation, execute, boundStoreSession, cancellationToken);

    public async Task<TResult> ExecuteAsync<TResult>(
        Operation operation,
        Func<OperationContext, Task<TResult>> execute,
        CancellationToken cancellationToken = default)
    {
        TResult? result = default;
        await ExecuteCoreAsync(operation, async context =>
        {
            result = await execute(context).ConfigureAwait(false);
        }, boundStoreSession, cancellationToken).ConfigureAwait(false);
        return result!;
    }

    /// <summary>
    /// Executes two application stages in one write transaction. Each stage receives a session
    /// bound to that transaction, and the first stage is flushed before the second begins so the
    /// continuation can query its post-mutation state. Nested calls participate in the caller's
    /// transaction; only the outer owner commits or rolls it back.
    /// </summary>
    public async Task<TResult> ExecuteAtomicAsync<TMutation, TResult>(
        Func<MixologySession, CancellationToken, Task<TMutation>> mutate,
        Func<MixologySession, TMutation, CancellationToken, Task<TResult>> continueWith,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        ArgumentNullException.ThrowIfNull(continueWith);
        using CancellationTokenSource? linked = LinkCancellation(cancellationToken);
        CancellationToken effective = linked?.Token
            ?? (cancellationToken.CanBeCanceled ? cancellationToken : applicationCancellation);

        if (boundStoreSession is { HasTransaction: true } callerSession)
        {
            return await ExecuteStagesAsync(
                callerSession,
                this,
                mutate,
                continueWith,
                effective).ConfigureAwait(false);
        }

        await using StoreSession ownedSession = await store.OpenSessionAsync(effective).ConfigureAwait(false);
        await ownedSession.BeginWriteAsync(effective).ConfigureAwait(false);
        MixologySession transactionSession = new(
            pipeline,
            store,
            Actor,
            applicationCancellation,
            ownedSession);
        try
        {
            TResult result = await ExecuteStagesAsync(
                ownedSession,
                transactionSession,
                mutate,
                continueWith,
                effective).ConfigureAwait(false);
            await ownedSession.CommitAsync(effective).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await ownedSession.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
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

    private static async Task<TResult> ExecuteStagesAsync<TMutation, TResult>(
        StoreSession storeSession,
        MixologySession transactionSession,
        Func<MixologySession, CancellationToken, Task<TMutation>> mutate,
        Func<MixologySession, TMutation, CancellationToken, Task<TResult>> continueWith,
        CancellationToken cancellationToken)
    {
        TMutation mutation = await mutate(transactionSession, cancellationToken).ConfigureAwait(false);
        await SaveAsync(storeSession, "persist atomic mutation", cancellationToken).ConfigureAwait(false);
        TResult result = await continueWith(transactionSession, mutation, cancellationToken).ConfigureAwait(false);
        await SaveAsync(storeSession, "persist atomic continuation", cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static async Task SaveAsync(
        StoreSession session,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await session.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw PersistenceErrors.TranslateSave(exception, operation);
        }
    }
}

public sealed class MixologySessionFactory(
    OperationPipeline pipeline,
    MixologyStore store,
    IHostApplicationLifetime? lifetime = null)
{
    public MixologySession Create(Actor actor) =>
        new(pipeline, store, actor, lifetime?.ApplicationStopping ?? default);
}
