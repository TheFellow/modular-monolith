namespace Mixology.Application.Operations;

public sealed class SerializationMiddleware
{
    public async Task InvokeAsync(OperationContext context, Operation operation, OperationDelegate next)
    {
        _ = operation;
        if (context.Session is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        await context.Session.SerializedAsync(async (_, _) =>
        {
            await next(context).ConfigureAwait(false);
            return true;
        }, context.CancellationToken).ConfigureAwait(false);
    }
}
