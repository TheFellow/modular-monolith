using Mixology.Kernel.Entities;
using Mixology.Persistence;

namespace Mixology.Application.Operations;

public sealed class OperationContext
{
    private readonly List<object> events;
    private readonly HashSet<EntityUid> touchedEntities;

    public OperationContext(
        string principal,
        StoreSession? session = null,
        CancellationToken cancellationToken = default)
        : this(principal, session, [], [], cancellationToken)
    {
    }

    private OperationContext(
        string principal,
        StoreSession? session,
        List<object> events,
        HashSet<EntityUid> touchedEntities,
        CancellationToken cancellationToken)
    {
        Principal = string.IsNullOrWhiteSpace(principal) ? "anonymous" : principal;
        CancellationToken = cancellationToken;
        Session = session;
        this.events = events;
        this.touchedEntities = touchedEntities;
    }

    public string Principal { get; }
    public CancellationToken CancellationToken { get; }
    public StoreSession? Session { get; }
    public IReadOnlyList<object> Events => events;
    public IReadOnlySet<EntityUid> TouchedEntities => touchedEntities;

    public void AddEvent(object domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        events.Add(domainEvent);
    }

    public void Touch(EntityUid entity) => touchedEntities.Add(entity);

    internal OperationContext ForOperation() => new(Principal, Session, [], [], CancellationToken);

    internal OperationContext WithSession(StoreSession session) =>
        new(Principal, session, events, touchedEntities, CancellationToken);
}

public sealed class EventHandlerContext
{
    private readonly OperationContext operation;

    internal EventHandlerContext(OperationContext operation)
    {
        this.operation = operation;
    }

    public string Principal => operation.Principal;
    public CancellationToken CancellationToken => operation.CancellationToken;
    public StoreSession Session => operation.Session
        ?? throw new InvalidOperationException("Event handler requires an active store session.");
    public void Touch(EntityUid entity) => operation.Touch(entity);
}
