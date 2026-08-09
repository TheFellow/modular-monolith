using Mixology.Application.Auditing;
using Mixology.Application.Events;

namespace Mixology.Application.Operations;

public sealed class OperationPipeline
{
    private readonly OperationChain command;
    private readonly OperationChain query;

    public OperationPipeline(
        SerializationMiddleware serialization,
        LoggingMiddleware logging,
        MetricsMiddleware metrics,
        TrackActivityMiddleware trackActivity,
        UnitOfWorkMiddleware unitOfWork,
        RecordSuccessfulActivityMiddleware recordSuccessfulActivity,
        DispatchEventsMiddleware dispatchEvents)
    {
        query = new([
            serialization.InvokeAsync,
            logging.InvokeAsync,
            metrics.InvokeAsync,
        ]);
        command = new([
            serialization.InvokeAsync,
            logging.InvokeAsync,
            metrics.InvokeAsync,
            trackActivity.InvokeAsync,
            unitOfWork.InvokeAsync,
            recordSuccessfulActivity.InvokeAsync,
            dispatchEvents.InvokeAsync,
        ]);
    }

    public Task ExecuteAsync(OperationContext context, Operation operation, OperationDelegate final) =>
        (operation.Kind == OperationKind.Command ? command : query).ExecuteAsync(context, operation, final);
}
