using System.Globalization;

namespace Mixology.Kernel.Measurement;

public readonly record struct Volume(double Milliliters)
{
    public const double OunceToMilliliters = 29.5735d;

    public static Volume FromMilliliters(double value) => new(value);
    public static Volume FromOunces(double value) => new(value * OunceToMilliliters);
    public static Volume FromCentiliters(double value) => new(value * 10d);

    public double Ounces => Milliliters / OunceToMilliliters;
    public double Centiliters => Milliliters / 10d;
    public bool IsZero => Milliliters == 0d;

    public Volume Add(Volume other) => new(Milliliters + other.Milliliters);
    public Volume Subtract(Volume other) => new(Milliliters - other.Milliliters);
    public Volume Multiply(double factor) => new(Milliliters * factor);
    public Volume Divide(double divisor) => new(Milliliters / divisor);

    public override string ToString() => Milliliters >= 100d
        ? $"{Milliliters.ToString("F0", CultureInfo.InvariantCulture)} ml"
        : $"{Ounces.ToString("F1", CultureInfo.InvariantCulture)} oz";
}

