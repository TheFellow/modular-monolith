using System.Text.Json;
using System.Text.Json.Serialization;
using Mixology.Kernel.Errors;

namespace Mixology.Kernel.Money;

[JsonConverter(typeof(CurrencyJsonConverter))]
public readonly record struct Currency
{
    private Currency(string code, string symbol, string name, string formatPattern)
    {
        Code = code;
        Symbol = symbol;
        Name = name;
        FormatPattern = formatPattern;
    }

    public string Code { get; } = string.Empty;

    public string Symbol { get; } = string.Empty;

    public string Name { get; } = string.Empty;

    public string FormatPattern { get; } = string.Empty;

    public static Currency Usd { get; } = new("USD", "$", "US Dollar", "${0}");

    public static Currency Eur { get; } = new("EUR", "€", "Euro", "{0} €");

    public bool IsEmpty => string.IsNullOrEmpty(Code);

    public static Currency Parse(string code) => code switch
    {
        "USD" => Usd,
        "EUR" => Eur,
        _ => throw AppError.Invalid($"unknown currency: {code}"),
    };

    public void Validate() => _ = Parse(Code);

    public string Format(string amount) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, FormatPattern, amount);

    public override string ToString() => Code;
}

public sealed class CurrencyJsonConverter : JsonConverter<Currency>
{
    public override Currency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("currency code is required");
        }

        string? code = reader.GetString();
        if (code is null)
        {
            throw new JsonException("currency code is required");
        }

        try
        {
            return Currency.Parse(code);
        }
        catch (AppError error)
        {
            throw new JsonException(error.Message, error);
        }
    }

    public override void Write(Utf8JsonWriter writer, Currency value, JsonSerializerOptions options)
    {
        if (value.IsEmpty)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Code);
    }
}

