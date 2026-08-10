using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Tags;
using Mixology.Modules.Menus.Models;
using Xunit;

namespace Mixology.Modules.Menus.Tests;

public sealed class MenuModelTests
{
    [Fact]
    public void LifecycleGuardsPreserveDraftPublishAndReturnToDraftRules()
    {
        Menu draft = Menu(MenuStatus.Draft, []);
        draft.RequireDraft();
        Assert.Throws<FailedPreconditionError>(draft.RequirePublishable);

        Menu publishable = Menu(MenuStatus.Draft, [Item()]);
        publishable.RequirePublishable();

        Menu published = publishable with { Status = MenuStatus.Published };
        published.RequireReturnToDraft();
        Assert.Throws<FailedPreconditionError>(published.RequireDraft);
    }

    [Fact]
    public void ReadinessReportsKeepWarningsButBlockOnEveryBlockerMessage()
    {
        MenuId id = MenuId.New();
        ReadinessReport warning = new(
            id,
            MenuStatus.Draft,
            [new ReadinessFinding(
                ReadinessSeverity.Warning,
                ReadinessCode.LowStock,
                DrinkId.New(),
                IngredientId.New(),
                "low stock")]);
        warning.RequireReady();

        ReadinessReport blocked = warning with
        {
            Findings =
            [
                .. warning.Findings,
                new ReadinessFinding(
                    ReadinessSeverity.Blocker,
                    ReadinessCode.Unavailable,
                    DrinkId.New(),
                    null,
                    "drink unavailable"),
            ],
        };
        FailedPreconditionError error = Assert.Throws<FailedPreconditionError>(blocked.RequireReady);

        Assert.True(blocked.HasBlockers);
        Assert.Contains("drink unavailable", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MenuRejectsDuplicateDrinksAndSortOrders()
    {
        MenuItem first = Item();
        Menu duplicateDrink = Menu(MenuStatus.Draft, [first, first with { SortOrder = 1 }]);
        Menu duplicateOrder = Menu(MenuStatus.Draft, [first, Item() with { SortOrder = 0 }]);

        Assert.Throws<InvalidError>(duplicateDrink.Normalize);
        Assert.Throws<InvalidError>(duplicateOrder.Normalize);
    }

    private static Menu Menu(MenuStatus status, IReadOnlyList<MenuItem> items) => new(
        MenuId.New(),
        "Service",
        string.Empty,
        items,
        status,
        DateTimeOffset.UtcNow,
        status == MenuStatus.Published ? DateTimeOffset.UtcNow : null,
        null,
        TagCollection.Empty);

    private static MenuItem Item() => new(
        DrinkId.New(),
        null,
        null,
        false,
        Availability.Available,
        0);
}
