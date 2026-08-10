using System.Numerics;
using Mixology.Kernel.Errors;

namespace Mixology.Kernel.Entities;

public static class EntityIds
{
    public const string DrinkType = "Mixology::Drink";
    public const string IngredientType = "Mixology::Ingredient";
    public const string InventoryType = "Mixology::Inventory";
    public const string MenuType = "Mixology::Menu";
    public const string OrderType = "Mixology::Order";
    public const string AuditEntryType = "Mixology::AuditEntry";

    internal const string DrinkPrefix = "drk";
    internal const string IngredientPrefix = "ing";
    internal const string InventoryPrefix = "inv";
    internal const string MenuPrefix = "mnu";
    internal const string OrderPrefix = "ord";
    internal const string AuditEntryPrefix = "aud";

    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static readonly BigInteger MaxKsuid = (BigInteger.One << 160) - BigInteger.One;

    public static EntityUid Parse(string value)
    {
        string prefix = PrefixOf(value);
        return prefix switch
        {
            DrinkPrefix => Parse(value, DrinkPrefix, DrinkType),
            IngredientPrefix => Parse(value, IngredientPrefix, IngredientType),
            InventoryPrefix => Parse(value, InventoryPrefix, InventoryType),
            MenuPrefix => Parse(value, MenuPrefix, MenuType),
            OrderPrefix => Parse(value, OrderPrefix, OrderType),
            AuditEntryPrefix => Parse(value, AuditEntryPrefix, AuditEntryType),
            _ => throw AppError.Invalid($"unsupported entity id prefix: {prefix}"),
        };
    }

    internal static string New(string prefix) => $"{prefix}-{KsuidDotNet.Ksuid.NewKsuid()}";

    internal static EntityUid Parse(string value, string prefix, string entityType)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw AppError.Invalid($"invalid {prefix} id: empty");
        }

        string expectedPrefix = $"{prefix}-";
        if (!value.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw AppError.Invalid($"invalid {prefix} id prefix: {value}");
        }

        string suffix = value[expectedPrefix.Length..];
        if (!IsCanonicalKsuid(suffix))
        {
            throw AppError.Invalid($"invalid {prefix} id: {value}");
        }

        return new EntityUid(entityType, value);
    }

    private static string PrefixOf(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw AppError.Invalid("invalid entity id: empty");
        }

        int separator = value.IndexOf('-', StringComparison.Ordinal);
        if (separator <= 0)
        {
            throw AppError.Invalid($"invalid entity id: {value}");
        }

        return value[..separator];
    }

    private static bool IsCanonicalKsuid(string value)
    {
        if (value.Length != 27)
        {
            return false;
        }

        BigInteger decoded = BigInteger.Zero;
        foreach (char character in value)
        {
            int digit = Base62Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (digit < 0)
            {
                return false;
            }

            decoded = (decoded * 62) + digit;
            if (decoded > MaxKsuid)
            {
                return false;
            }
        }

        return true;
    }
}

