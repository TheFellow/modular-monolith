using Mixology.Application.Operations;
using Mixology.Kernel.Errors;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Persistence;
using Mixology.Modules.Menus.Ports;

namespace Mixology.Modules.Menus.Handlers;

internal static class MenuAvailabilityReaction
{
    public static async Task<bool> RecalculateAsync(
        EventHandlerContext context,
        MenuRow menu,
        IMenuOperations operations,
        IReadOnlySet<string>? selectedDrinkIds = null)
    {
        bool changed = false;
        foreach (MenuItemRow item in menu.Items.OrderBy(static item => item.SortOrder))
        {
            if (selectedDrinkIds is not null && !selectedDrinkIds.Contains(item.DrinkId))
            {
                continue;
            }

            Availability availability = await CalculateAsync(
                context,
                operations,
                item.DrinkId).ConfigureAwait(false);
            if (string.Equals(item.Availability, availability.Value, StringComparison.Ordinal))
            {
                continue;
            }

            item.Availability = availability.Value;
            changed = true;
        }

        return changed;
    }

    private static async ValueTask<Availability> CalculateAsync(
        EventHandlerContext context,
        IMenuOperations operations,
        string drinkId)
    {
        try
        {
            Availability availability = await operations.GetAvailabilityAsync(
                context.Session,
                Mixology.Kernel.Entities.DrinkId.Parse(drinkId),
                context.CancellationToken).ConfigureAwait(false);
            availability.Validate();
            return availability;
        }
        catch (Exception exception) when (!AppError.IsCancellation(exception))
        {
            return Availability.Unavailable;
        }
    }
}
