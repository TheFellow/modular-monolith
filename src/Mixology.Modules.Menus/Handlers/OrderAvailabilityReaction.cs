using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Modules.Menus.Persistence;
using Mixology.Modules.Menus.Ports;

namespace Mixology.Modules.Menus.Handlers;

internal static class OrderAvailabilityReaction
{
    public static async Task RecalculatePublishedAsync(
        EventHandlerContext context,
        IMenuOperations operations)
    {
        MenuRow[] menus = await MenuEventPersistence.LoadPublishedAsync(context).ConfigureAwait(false);
        foreach (MenuRow menu in menus)
        {
            if (!await MenuAvailabilityReaction.RecalculateAsync(context, menu, operations).ConfigureAwait(false))
            {
                continue;
            }

            context.Touch(MenuId.Parse(menu.Id).EntityUid);
        }
    }
}
