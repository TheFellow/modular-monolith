using Mixology.Modules.Orders.Models;

namespace Mixology.Modules.Orders.Events;

public sealed record OrderPlaced(Order Order);

public sealed record OrderCompleted(Order Order);

public sealed record OrderCancelled(Order Order);
