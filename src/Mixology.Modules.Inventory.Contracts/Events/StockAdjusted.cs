using Mixology.Modules.Inventory.Models;

namespace Mixology.Modules.Inventory.Events;

public sealed record StockAdjusted(InventoryStock Inventory, string Reason, bool Shortage);
