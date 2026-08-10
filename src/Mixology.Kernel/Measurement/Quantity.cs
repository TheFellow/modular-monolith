using System.Globalization;
using Mixology.Kernel.Errors;

namespace Mixology.Kernel.Measurement;

public readonly record struct Quantity
{
    public Quantity(double value, Unit unit)
    {
        unit.Validate();
        if (!unit.IsVolume)
        {
            throw AppError.Invalid($"unit \"{unit}\" is not a volume");
        }

        Volume = unit == Unit.Ounce
            ? Volume.FromOunces(value)
            : unit == Unit.Centiliter
                ? Volume.FromCentiliters(value)
                : Volume.FromMilliliters(value);
        Unit = unit;
    }

    private Quantity(Volume volume, Unit unit)
    {
        Volume = volume;
        Unit = unit;
    }

    public Volume Volume { get; }

    public Unit Unit { get; }

    public double Value => Unit == Unit.Ounce
        ? Volume.Ounces
        : Unit == Unit.Centiliter
            ? Volume.Centiliters
            : Volume.Milliliters;

    public bool IsZero => Volume.IsZero;

    public Quantity Convert(Unit unit)
    {
        unit.Validate();
        if (!unit.IsVolume)
        {
            throw AppError.Invalid($"unit \"{unit}\" is not a volume");
        }

        return new Quantity(Volume, unit);
    }

    public Quantity Add(Quantity other) => new(Volume.Add(other.Volume), Unit);
    public Quantity Subtract(Quantity other) => new(Volume.Subtract(other.Volume), Unit);
    public Quantity Multiply(double factor) => new(Volume.Multiply(factor), Unit);
    public Quantity Divide(double divisor) => new(Volume.Divide(divisor), Unit);
    public bool LessThan(Quantity other) => Volume.Milliliters < other.Volume.Milliliters;

    public override string ToString() =>
        $"{Value.ToString("F2", CultureInfo.InvariantCulture)} {Unit}";
}

