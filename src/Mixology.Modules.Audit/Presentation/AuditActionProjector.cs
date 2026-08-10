using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Modules.Audit.Authorization;
using Mixology.Modules.Audit.Models;

namespace Mixology.Modules.Audit.Presentation;

public sealed class AuditActionProjector(IEntityAuthorizer authorizer)
{
    public static ActionId ListAction { get; } = new("audit.list");
    public static ActionId ViewAction { get; } = new("audit.view");

    public Task<IReadOnlyList<ActionState>> ProjectAsync(
        Actor principal,
        AuditEntry? selected = null,
        CancellationToken cancellationToken = default)
    {
        ActionControl list = new(
            ListAction,
            Require(principal, AuditAuthorization.List, AuditAuthorization.ToCedarEntity(
                new EntityUid(EntityIds.AuditEntryType, "workspace"))));
        if (selected is null)
        {
            return ActionProjector.EvaluateAsync(new ActionGroup(Controls: [list]), cancellationToken);
        }

        return ActionProjector.EvaluateAsync(
            new ActionGroup(Controls:
            [
                list,
                new ActionControl(
                    ViewAction,
                    Require(principal, AuditAuthorization.Get, selected.ToCedarEntity())),
            ]),
            cancellationToken);
    }

    private ActionPermission Require(Actor principal, EntityUid action, Cedar.Types.Entity resource) =>
        ActionPermission.Require(token => authorizer.AuthorizeAsync(principal, action, resource, token));
}
