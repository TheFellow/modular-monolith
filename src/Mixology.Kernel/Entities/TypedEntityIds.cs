namespace Mixology.Kernel.Entities;

public readonly record struct DrinkId(string Value) : IEntityId
{
    public EntityUid EntityUid => EntityIds.Parse(Value, EntityIds.DrinkPrefix, EntityIds.DrinkType);
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static DrinkId New() => new(EntityIds.New(EntityIds.DrinkPrefix));
    public static DrinkId Parse(string value) => new(EntityIds.Parse(value, EntityIds.DrinkPrefix, EntityIds.DrinkType).Id);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct IngredientId(string Value) : IEntityId
{
    public EntityUid EntityUid => EntityIds.Parse(Value, EntityIds.IngredientPrefix, EntityIds.IngredientType);
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static IngredientId New() => new(EntityIds.New(EntityIds.IngredientPrefix));
    public static IngredientId Parse(string value) => new(EntityIds.Parse(value, EntityIds.IngredientPrefix, EntityIds.IngredientType).Id);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct InventoryId(string Value) : IEntityId
{
    public EntityUid EntityUid => EntityIds.Parse(Value, EntityIds.InventoryPrefix, EntityIds.InventoryType);
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static InventoryId New() => new(EntityIds.New(EntityIds.InventoryPrefix));
    public static InventoryId Parse(string value) => new(EntityIds.Parse(value, EntityIds.InventoryPrefix, EntityIds.InventoryType).Id);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct MenuId(string Value) : IEntityId
{
    public EntityUid EntityUid => EntityIds.Parse(Value, EntityIds.MenuPrefix, EntityIds.MenuType);
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static MenuId New() => new(EntityIds.New(EntityIds.MenuPrefix));
    public static MenuId Parse(string value) => new(EntityIds.Parse(value, EntityIds.MenuPrefix, EntityIds.MenuType).Id);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct OrderId(string Value) : IEntityId
{
    public EntityUid EntityUid => EntityIds.Parse(Value, EntityIds.OrderPrefix, EntityIds.OrderType);
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static OrderId New() => new(EntityIds.New(EntityIds.OrderPrefix));
    public static OrderId Parse(string value) => new(EntityIds.Parse(value, EntityIds.OrderPrefix, EntityIds.OrderType).Id);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct AuditEntryId(string Value) : IEntityId
{
    public EntityUid EntityUid => EntityIds.Parse(Value, EntityIds.AuditEntryPrefix, EntityIds.AuditEntryType);
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static AuditEntryId New() => new(EntityIds.New(EntityIds.AuditEntryPrefix));
    public static AuditEntryId Parse(string value) => new(EntityIds.Parse(value, EntityIds.AuditEntryPrefix, EntityIds.AuditEntryType).Id);
    public override string ToString() => Value ?? string.Empty;
}

