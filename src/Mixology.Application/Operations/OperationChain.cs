namespace Mixology.Application.Operations;

public delegate Task OperationDelegate(OperationContext context);

public delegate Task OperationMiddleware(OperationContext context, Operation operation, OperationDelegate next);

public sealed class OperationChain(IEnumerable<OperationMiddleware> middleware)
{
    private readonly OperationMiddleware[] middleware = middleware.ToArray();

    public Task ExecuteAsync(OperationContext context, Operation operation, OperationDelegate final)
    {
        OperationDelegate next = final;
        for (int index = middleware.Length - 1; index >= 0; index--)
        {
            OperationMiddleware current = middleware[index];
            OperationDelegate inner = next;
            next = operationContext => current(operationContext, operation, inner);
        }

        return next(context.ForOperation());
    }
}
