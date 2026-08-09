using Cedar.Types;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Presentation;
using Mixology.Modules.Menus.Authorization;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Presentation;
using Mixology.Modules.Orders.Authorization;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Presentation;
using Xunit;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;
using Order = Mixology.Modules.Orders.Models.Order;

namespace Mixology.Presentation.Tests;

public sealed class DomainActionProjectorTests
{
    [Fact]
    public async Task MenuUsesIndependentActionOverridesAndLifecycleConditions()
    {
        RecordingAuthorizer authorizer = new(action =>
            action == MenuAuthorization.Update ? AppError.Permission("update denied") : null);
        MenuActionProjector projector = new(authorizer);

        IReadOnlyList<ActionState> states = await projector.ProjectAsync(Actor.Manager, Menu(MenuStatus.Draft, 1));
        Dictionary<ActionId, ActionState> byId = states.ToDictionary(static state => state.Id);

        Assert.False(byId[MenuActionProjector.EditAction].Visible);
        Assert.Empty(byId[MenuActionProjector.EditAction].DisabledReason);
        Assert.True(byId[MenuActionProjector.PublishAction].Enabled);
        Assert.True(byId[MenuActionProjector.TagsAction].Enabled);
        Assert.False(byId[MenuActionProjector.DraftAction].Enabled);
        Assert.Equal("Available only while the menu is published.", byId[MenuActionProjector.DraftAction].DisabledReason);
    }

    [Fact]
    public async Task MenuReadinessOnlyDisablesVisibleEnabledPublish()
    {
        MenuActionProjector projector = new(new RecordingAuthorizer());
        Menu menu = Menu(MenuStatus.Draft, 1);
        IReadOnlyList<ActionState> states = await projector.ProjectAsync(Actor.Owner, menu);
        ReadinessReport report = new(
            menu.Id,
            menu.Status,
            [new ReadinessFinding(
                ReadinessSeverity.Blocker,
                ReadinessCode.Unavailable,
                menu.Items[0].DrinkId,
                null,
                "unavailable")]);

        IReadOnlyList<ActionState> composed = MenuActionProjector.ApplyReadiness(states, report);

        Assert.True(states.Single(state => state.Id == MenuActionProjector.PublishAction).Enabled);
        ActionState publish = composed.Single(state => state.Id == MenuActionProjector.PublishAction);
        Assert.False(publish.Enabled);
        Assert.Equal("Resolve menu readiness blockers before publishing.", publish.DisabledReason);
    }

    [Fact]
    public async Task OrderListCapabilityIsAuthorizedInsteadOfPublic()
    {
        RecordingAuthorizer authorizer = new(action =>
            action == OrderAuthorization.List ? AppError.Permission("list denied") : null);
        OrderActionProjector projector = new(authorizer);

        IReadOnlyList<ActionState> states = await projector.ProjectAsync(Actor.Anonymous);

        Assert.False(states.Single(state => state.Id == OrderActionProjector.ListAction).Visible);
        Assert.True(states.Single(state => state.Id == OrderActionProjector.PlaceAction).Visible);
    }

    [Theory]
    [InlineData("pending", true, true)]
    [InlineData("blocked", false, true)]
    [InlineData("completed", false, false)]
    [InlineData("cancelled", false, false)]
    public async Task OrderLifecycleProducesVisibleDisabledReasons(
        string status,
        bool complete,
        bool cancel)
    {
        OrderActionProjector projector = new(new RecordingAuthorizer());

        IReadOnlyList<ActionState> states = await projector.ProjectAsync(
            Actor.Owner,
            Order(OrderStatus.Parse(status)));
        Dictionary<ActionId, ActionState> byId = states.ToDictionary(static state => state.Id);

        Assert.Equal(complete, byId[OrderActionProjector.CompleteAction].Enabled);
        Assert.Equal(cancel, byId[OrderActionProjector.CancelAction].Enabled);
        Assert.True(byId[OrderActionProjector.CompleteAction].Visible);
        Assert.True(byId[OrderActionProjector.CancelAction].Visible);
        if (!complete)
        {
            Assert.NotEmpty(byId[OrderActionProjector.CompleteAction].DisabledReason);
        }
    }

    [Fact]
    public async Task ProjectorPreservesTypedEvaluatorFailure()
    {
        ConflictError expected = AppError.Conflict("policy evaluator unavailable");
        OrderActionProjector projector = new(new RecordingAuthorizer(action =>
            action == OrderAuthorization.Place ? expected : null));

        ConflictError actual = await Assert.ThrowsAsync<ConflictError>(
            () => projector.ProjectAsync(Actor.Owner));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task DrinkCreateUsesEmptyProjectionResourceForCedarCategoryPolicy()
    {
        await using ServiceProvider services = new ServiceCollection()
            .AddDrinksModule()
            .BuildServiceProvider();
        DrinkActionProjector projector = services.GetRequiredService<DrinkActionProjector>();

        ActionState bartender = (await projector.ProjectAsync(Actor.Bartender))
            .Single(state => state.Id == DrinkActionProjector.CreateAction);
        ActionState sommelier = (await projector.ProjectAsync(Actor.Sommelier))
            .Single(state => state.Id == DrinkActionProjector.CreateAction);

        Assert.True(bartender.Visible);
        Assert.True(bartender.Enabled);
        Assert.False(sommelier.Visible);
        Assert.False(sommelier.Enabled);
    }

    private static Menu Menu(MenuStatus status, int itemCount)
    {
        MenuItem[] items = Enumerable.Range(0, itemCount).Select(index => new MenuItem(
            DrinkId.New(),
            $"Drink {index}",
            null,
            false,
            Availability.Available,
            index)).ToArray();
        return new Menu(
            MenuId.New(),
            "Actions",
            string.Empty,
            items,
            status,
            DateTimeOffset.UnixEpoch,
            status == MenuStatus.Published ? DateTimeOffset.UnixEpoch : null,
            null,
            TagCollection.Empty);
    }

    private static Order Order(OrderStatus status) => new(
        OrderId.New(),
        MenuId.New(),
        [],
        [],
        [],
        status,
        DateTimeOffset.UnixEpoch,
        status == OrderStatus.Completed ? DateTimeOffset.UnixEpoch : null,
        string.Empty,
        null,
        TagCollection.Empty);

    internal sealed class RecordingAuthorizer(
        Func<KernelEntityUid, Exception?>? failure = null) : IEntityAuthorizer
    {
        public List<KernelEntityUid> Actions { get; } = [];

        public ValueTask AuthorizeAsync(
            Actor principal,
            KernelEntityUid action,
            Entity resource,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Actions.Add(action);
            if (failure?.Invoke(action) is { } exception)
            {
                throw exception;
            }

            return ValueTask.CompletedTask;
        }
    }
}
