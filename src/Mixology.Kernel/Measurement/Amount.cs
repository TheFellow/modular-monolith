using Mixology.Kernel.Errors;

namespace Mixology.Kernel.Measurement;

public abstract record Amount
{
    private protected Amount()
    {
    }

    public abstract Unit Unit { get; }
    public abstract double Value { get; }
    public abstract bool IsZero { get; }

    public static Amount Create(double value, Unit unit)
    {
        unit.Validate();
        return unit.IsVolume
            ? new VolumeAmount(new Quantity(value, unit))
            : new DiscreteAmount(new DiscreteQuantity(value, unit));
    }

    public abstract Amount Convert(Unit unit);
    public abstract Amount Add(Amount other);
    public abstract Amount Subtract(Amount other);
    public abstract Amount Multiply(double factor);
    public abstract bool LessThan(Amount other);
}

public sealed record VolumeAmount(Quantity Quantity) : Amount
{
    public override Unit Unit => Quantity.Unit;
    public override double Value => Quantity.Value;
    public override bool IsZero => Quantity.IsZero;

    public override Amount Convert(Unit unit) => new VolumeAmount(Quantity.Convert(unit));

    public override Amount Add(Amount other) => other is VolumeAmount volume
        ? new VolumeAmount(Quantity.Add(volume.Quantity))
        : throw Mismatch(other);

    public override Amount Subtract(Amount other) => other is VolumeAmount volume
        ? new VolumeAmount(Quantity.Subtract(volume.Quantity))
        : throw Mismatch(other);

    public override Amount Multiply(double factor) => new VolumeAmount(Quantity.Multiply(factor));

    public override bool LessThan(Amount other) => other is VolumeAmount volume
        ? Quantity.LessThan(volume.Quantity)
        : throw Mismatch(other);

    public override string ToString() => Quantity.ToString();

    private AppError Mismatch(Amount? other) => other is null
        ? AppError.Invalid("amount is empty")
        : AppError.Invalid($"unit mismatch: {Unit} vs {other.Unit}");
}

public sealed record DiscreteAmount(DiscreteQuantity Quantity) : Amount
{
    public override Unit Unit => Quantity.Unit;
    public override double Value => Quantity.Count;
    public override bool IsZero => Quantity.IsZero;

    public override Amount Convert(Unit unit) => unit == Unit
        ? this
        : throw AppError.Invalid($"unit mismatch: {Unit} vs {unit}");

    public override Amount Add(Amount other) => other is DiscreteAmount discrete
        ? new DiscreteAmount(Quantity.Add(discrete.Quantity))
        : throw Mismatch(other);

    public override Amount Subtract(Amount other) => other is DiscreteAmount discrete
        ? new DiscreteAmount(Quantity.Subtract(discrete.Quantity))
        : throw Mismatch(other);

    public override Amount Multiply(double factor) => new DiscreteAmount(Quantity.Multiply(factor));

    public override bool LessThan(Amount other) => other is DiscreteAmount discrete
        ? Quantity.LessThan(discrete.Quantity)
        : throw Mismatch(other);

    public override string ToString() => Quantity.ToString();

    private AppError Mismatch(Amount? other) => other is null
        ? AppError.Invalid("amount is empty")
        : AppError.Invalid($"unit mismatch: {Unit} vs {other.Unit}");
}
