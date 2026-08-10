using Mixology.Modules.Drinks.Models;

namespace Mixology.Modules.Drinks.Events;

public sealed record DrinkCreated(Drink Drink);
public sealed record DrinkUpdated(Drink Drink);
public sealed record DrinkDeleted(Drink Drink, DateTimeOffset DeletedAt);
