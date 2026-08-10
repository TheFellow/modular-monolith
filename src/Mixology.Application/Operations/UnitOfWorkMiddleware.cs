using Mixology.Persistence;

namespace Mixology.Application.Operations;

public sealed class UnitOfWorkMiddleware(MixologyStore store)
{
    public async Task InvokeAsync(OperationContext context, Operation operation, OperationDelegate next)
    {
        if (operation.Kind != OperationKind.Command)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (context.Session is { HasTransaction: true })
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (context.Session is { } suppliedSession)
        {
            await ExecuteTransactionAsync(context, suppliedSession, next).ConfigureAwait(false);
            return;
        }

        await using StoreSession session = await store.OpenSessionAsync(context.CancellationToken).ConfigureAwait(false);
        await ExecuteTransactionAsync(context, session, next).ConfigureAwait(false);
    }

    private static async Task ExecuteTransactionAsync(
        OperationContext context,
        StoreSession session,
        OperationDelegate next)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        await session.BeginWriteAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await next(context.WithSession(session)).ConfigureAwait(false);
            try
            {
                await session.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw PersistenceErrors.TranslateSave(exception, $"persist {context.Activity?.Operation.Action ?? "command"}");
            }

            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await session.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
