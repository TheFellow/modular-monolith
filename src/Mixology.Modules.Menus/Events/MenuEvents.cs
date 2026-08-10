using Mixology.Modules.Menus.Models;

namespace Mixology.Modules.Menus.Events;

public sealed record MenuCreated(Menu Menu);
public sealed record DrinkAddedToMenu(Menu Menu, MenuItem Item);
public sealed record DrinkRemovedFromMenu(Menu Menu, MenuItem Item);
public sealed record MenuPublished(Menu Menu);
public sealed record MenuDrafted(Menu Menu);
