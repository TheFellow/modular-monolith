using Cedar.Types;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Modules.Tagging.Authorization;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Tagging.Presentation;

public sealed class TaggingActionProjector(
    IEntityAuthorizer authorizer,
    TagTargetRegistry registry)
{
    public static ActionId InspectAction { get; } = new("tagging.inspect");
    public static ActionId TagAction { get; } = new("tagging.tag");
    public static ActionId UntagAction { get; } = new("tagging.untag");
    public static ActionId ShowAction { get; } = new("tagging.show");
    public static ActionId SummaryAction { get; } = new("tagging.summary");

    public Task<IReadOnlyList<ActionState>> ProjectDiscoveryAsync(
        Actor principal,
        CancellationToken cancellationToken = default) =>
        ActionProjector.EvaluateAsync(
            new ActionGroup(Controls:
            [
                new ActionControl(
                    ShowAction,
                    Require(principal, TaggingAuthorization.Show, TaggingAuthorization.DiscoveryResource("show"))),
                new ActionControl(
                    SummaryAction,
                    Require(principal, TaggingAuthorization.Summary, TaggingAuthorization.DiscoveryResource("summary"))),
            ]),
            cancellationToken);

    public Task<IReadOnlyList<ActionState>> ProjectTargetAsync(
        Actor principal,
        Entity target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        Models.TagTargetRegistration registration = registry.Resolve(target.Uid.Type.Value);
        return ActionProjector.EvaluateAsync(
            new ActionGroup(Controls:
            [
                new ActionControl(InspectAction, Require(principal, registration.GetAction, target)),
                new ActionControl(TagAction, Require(principal, registration.TagAction, target)),
                new ActionControl(UntagAction, Require(principal, registration.UntagAction, target)),
            ]),
            cancellationToken);
    }

    private ActionPermission Require(Actor principal, KernelEntityUid action, Entity resource) =>
        ActionPermission.Require(token => authorizer.AuthorizeAsync(principal, action, resource, token));
}
