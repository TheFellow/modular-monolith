using System.Globalization;
using Mixology.Kernel.Errors;

namespace Mixology.Kernel.Measurement;

public readonly record struct DiscreteQuantity
{
    public DiscreteQuantity(double count, Unit unit)
    {
        unit.Validate();
        if (!unit.IsDiscrete)
        {
            throw AppError.Invalid($"unit \"{unit}\" is not discrete");
        }

        Count = count;
        Unit = unit;
    }

    public double Count { get; }

    public Unit Unit { get; }

    public bool IsZero => Count == 0d;

    public DiscreteQuantity Add(DiscreteQuantity other)
    {
        RequireSameUnit(other);
        return new DiscreteQuantity(Count + other.Count, Unit);
    }

    public DiscreteQuantity Subtract(DiscreteQuantity other)
    {
        RequireSameUnit(other);
        return new DiscreteQuantity(Count - other.Count, Unit);
    }

    public DiscreteQuantity Multiply(double factor) => new(Count * factor, Unit);

    public bool LessThan(DiscreteQuantity other)
    {
        RequireSameUnit(other);
        return Count < other.Count;
    }

    public override string ToString()
    {
        string count = Count.ToString("F0", CultureInfo.InvariantCulture);
        return Count == 1d ? $"1 {Unit}" : $"{count} {Unit}s";
    }

    private void RequireSameUnit(DiscreteQuantity other)
    {
        if (Unit != other.Unit)
        {
            throw AppError.Invalid($"unit mismatch: {Unit} vs {other.Unit}");
        }
    }
}

