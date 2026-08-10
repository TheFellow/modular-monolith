using Mixology.Application.Auditing;
using Mixology.Application.Authentication;
using Mixology.Kernel.Entities;
using Mixology.Persistence;

namespace Mixology.Application.Operations;

public sealed class OperationContext
{
    private readonly List<object> events;
    private readonly List<EntityUid> touchedEntities;
    private readonly HashSet<EntityUid> touchedEntitySet;

    public OperationContext(
        Actor principal,
        StoreSession? session = null,
        CancellationToken cancellationToken = default)
        : this(principal, session, [], [], [], cancellationToken)
    {
    }

    private OperationContext(
        Actor principal,
        StoreSession? session,
        List<object> events,
        List<EntityUid> touchedEntities,
        HashSet<EntityUid> touchedEntitySet,
        CancellationToken cancellationToken,
        OperationActivity? activity = null)
    {
        Principal = principal.IsEmpty ? Actor.Anonymous : principal;
        CancellationToken = cancellationToken;
        Session = session;
        this.events = events;
        this.touchedEntities = touchedEntities;
        this.touchedEntitySet = touchedEntitySet;
        Activity = activity;
    }

    public Actor Principal { get; }
    public CancellationToken CancellationToken { get; }
    public StoreSession? Session { get; }
    public IReadOnlyList<object> Events => events;
    public IReadOnlyList<EntityUid> TouchedEntities => touchedEntities;
    public OperationActivity? Activity { get; private set; }

    public void AddEvent(object domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        events.Add(domainEvent);
    }

    public void Touch(EntityUid entity)
    {
        if (touchedEntitySet.Add(entity))
        {
            touchedEntities.Add(entity);
        }
    }

    public void SelectResource(EntityUid entity)
    {
        if (Activity is not null && Activity.Resource is null)
        {
            Activity.Resource = entity;
        }
    }

    internal OperationContext ForOperation() => new(Principal, Session, [], [], [], CancellationToken);

    internal OperationContext WithSession(StoreSession session) =>
        new(Principal, session, events, touchedEntities, touchedEntitySet, CancellationToken, Activity);

    internal void StartActivity(OperationActivity activity) => Activity = activity;
}

public sealed class EventHandlerContext
{
    private readonly OperationContext operation;

    internal EventHandlerContext(OperationContext operation)
    {
        this.operation = operation;
    }

    public Actor Principal => operation.Principal;
    public CancellationToken CancellationToken => operation.CancellationToken;
    public StoreSession Session => operation.Session
        ?? throw new InvalidOperationException("Event handler requires an active store session.");
    public void Touch(EntityUid entity) => operation.Touch(entity);

    public async Task FlushAsync(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        try
        {
            await Session.Context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw PersistenceErrors.TranslateSave(exception, operationName);
        }
    }
}
