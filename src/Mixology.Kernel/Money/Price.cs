using System.Globalization;
using Mixology.Kernel.Errors;

namespace Mixology.Kernel.Money;

public readonly record struct Price
{
    public Price(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
        Validate();
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public bool IsZero => Amount == 0m;

    public bool IsNegative => Amount < 0m;

    public static Price FromCents(int cents, Currency currency) => new(cents / 100m, currency);

    public static Price Parse(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0)
        {
            throw AppError.Invalid("price is required");
        }

        if (raw.StartsWith('$'))
        {
            return FromTextAmount(raw[1..], Currency.Usd);
        }

        string[] parts = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw InvalidPrice(raw);
        }

        string code = parts[0];
        string amount = parts[1];
        if (!LooksLikeCurrencyCode(code))
        {
            (code, amount) = (amount, code);
        }

        if (!LooksLikeCurrencyCode(code))
        {
            throw InvalidPrice(raw);
        }

        return FromTextAmount(amount, Currency.Parse(code.ToUpperInvariant()));
    }

    public void Validate()
    {
        if (Amount < 0m)
        {
            throw AppError.Invalid("amount must be >= 0");
        }

        Currency.Validate();
    }

    public int Cents()
    {
        Validate();
        decimal rounded = decimal.Round(Amount, 2, MidpointRounding.AwayFromZero);
        try
        {
            return checked((int)(rounded * 100m));
        }
        catch (OverflowException exception)
        {
            throw AppError.Invalid("amount out of range", exception);
        }
    }

    public Price Add(Price other)
    {
        Validate();
        other.Validate();
        if (Currency.Code != other.Currency.Code)
        {
            throw AppError.Invalid($"currency mismatch: {Currency.Code} vs {other.Currency.Code}");
        }

        return new Price(Amount + other.Amount, Currency);
    }

    public Price Multiply(decimal factor)
    {
        Validate();
        if (factor < 0m)
        {
            throw AppError.Invalid("factor must be >= 0");
        }

        return new Price(Amount * factor, Currency);
    }

    public Price SuggestedPrice(double targetMargin)
    {
        Validate();
        if (targetMargin <= 0d || targetMargin >= 1d)
        {
            throw AppError.Invalid("target margin must be between 0 and 1");
        }

        int basisPoints = checked((int)Math.Floor((targetMargin * 10_000d) + 0.5d));
        if (basisPoints is <= 0 or >= 10_000)
        {
            throw AppError.Invalid("target margin must be between 0 and 1");
        }

        decimal divisor = (10_000m - basisPoints) / 10_000m;
        decimal suggested = decimal.Ceiling((Amount / divisor) * 100m) / 100m;
        return new Price(suggested, Currency);
    }

    public override string ToString()
    {
        if (Currency.IsEmpty)
        {
            return "?";
        }

        string amount = decimal.Round(Amount, 2, MidpointRounding.AwayFromZero)
            .ToString("F2", CultureInfo.InvariantCulture);
        return Currency.Format(amount);
    }

    private static Price FromTextAmount(string amount, Currency currency)
    {
        if (!decimal.TryParse(amount.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
        {
            throw AppError.Invalid($"invalid amount: {amount.Trim()}");
        }

        return new Price(parsed, currency);
    }

    private static bool LooksLikeCurrencyCode(string value) =>
        value.Length == 3 && value.All(char.IsLetter);

    private static AppError InvalidPrice(string raw) =>
        AppError.Invalid($"invalid price \"{raw}\" (expected \"$1.23\" or \"USD 1.23\" or \"1.23 USD\")");
}

