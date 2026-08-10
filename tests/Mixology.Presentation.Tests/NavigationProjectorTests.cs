using Mixology.Application.Authentication;
using Mixology.Kernel.Errors;
using Mixology.Modules.Audit.Authorization;
using Mixology.Modules.Audit.Presentation;
using Mixology.Modules.Drinks.Presentation;
using Mixology.Modules.Ingredients.Presentation;
using Mixology.Modules.Inventory.Presentation;
using Mixology.Modules.Menus.Presentation;
using Mixology.Modules.Orders.Presentation;
using Mixology.Modules.Tagging;
using Mixology.Modules.Tagging.Authorization;
using Mixology.Modules.Tagging.Presentation;
using Mixology.Presentation.Navigation;
using Xunit;

namespace Mixology.Presentation.Tests;

public sealed class NavigationProjectorTests
{
    [Fact]
    public async Task DenialsHideWorkspaceWhileOperationalErrorsKeepItDiscoverable()
    {
        DomainActionProjectorTests.RecordingAuthorizer authorizer = new(action =>
            action == AuditAuthorization.List
                ? AppError.Conflict("audit evaluator unavailable")
                : action == TaggingAuthorization.Summary
                    ? AppError.Permission("tags denied")
                    : null);
        NavigationProjector projector = new(
            new DrinkActionProjector(authorizer),
            new IngredientActionProjector(authorizer),
            new InventoryActionProjector(authorizer),
            new MenuActionProjector(authorizer),
            new OrderActionProjector(authorizer),
            new AuditActionProjector(authorizer),
            new TaggingActionProjector(authorizer, new TagTargetRegistry()));

        NavigationProjection projection = await projector.ProjectAsync(Actor.Manager);
        WorkspaceId[] ids = projection.Items.Select(static item => item.Id).ToArray();

        Assert.Contains(NavigationProjector.DashboardWorkspace, ids);
        Assert.Contains(NavigationProjector.AuditWorkspace, ids);
        Assert.DoesNotContain(NavigationProjector.TagsWorkspace, ids);
        Assert.IsType<ConflictError>(Assert.Single(projection.Errors));
    }

    [Fact]
    public async Task UnknownEvaluatorFailureBecomesSafeInternalAndKeepsWorkspaceVisible()
    {
        IOException cause = new("policy socket path must not leak");
        DomainActionProjectorTests.RecordingAuthorizer authorizer = new(action =>
            action == AuditAuthorization.List ? cause : null);
        NavigationProjector projector = Create(authorizer);

        NavigationProjection projection = await projector.ProjectAsync(Actor.Owner);

        Assert.Contains(
            projection.Items,
            item => item.Id == NavigationProjector.AuditWorkspace);
        InternalError error = Assert.IsType<InternalError>(Assert.Single(projection.Errors));
        Assert.Equal("internal error", error.UserMessage);
        Assert.Same(cause, error.InnerException);
    }

    private static NavigationProjector Create(
        DomainActionProjectorTests.RecordingAuthorizer authorizer) => new(
        new DrinkActionProjector(authorizer),
        new IngredientActionProjector(authorizer),
        new InventoryActionProjector(authorizer),
        new MenuActionProjector(authorizer),
        new OrderActionProjector(authorizer),
        new AuditActionProjector(authorizer),
        new TaggingActionProjector(authorizer, new TagTargetRegistry()));
}
