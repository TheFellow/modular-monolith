using Microsoft.EntityFrameworkCore.Storage;
using Mixology.Kernel.Errors;

namespace Mixology.Persistence;

public sealed class StoreSession : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IDbContextTransaction? transaction;
    private bool disposed;

    internal StoreSession(MixologyDbContext context)
    {
        Context = context;
    }

    public MixologyDbContext Context { get; }

    public bool HasTransaction => transaction is not null;

    public async Task BeginWriteAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (transaction is not null)
        {
            throw AppError.Internal("store session already has a transaction");
        }

        transaction = await Context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        IDbContextTransaction active = RequireTransaction();
        await active.CommitAsync(cancellationToken).ConfigureAwait(false);
        await active.DisposeAsync().ConfigureAwait(false);
        transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        IDbContextTransaction active = RequireTransaction();
        await active.RollbackAsync(cancellationToken).ConfigureAwait(false);
        await active.DisposeAsync().ConfigureAwait(false);
        transaction = null;
        Context.ChangeTracker.Clear();
    }

    public async Task<T> SerializedAsync<T>(Func<MixologyDbContext, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation(Context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (transaction is not null)
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
            await transaction.DisposeAsync().ConfigureAwait(false);
            transaction = null;
        }

        await Context.DisposeAsync().ConfigureAwait(false);
        gate.Dispose();
    }

    private IDbContextTransaction RequireTransaction() =>
        transaction ?? throw AppError.Internal("store session is missing a transaction");
}

