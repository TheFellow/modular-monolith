using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;

namespace Mixology.Modules.Ingredients.Models;

public sealed record Retirement(IngredientId? ReplacementId = null, double Ratio = 0)
{
    public bool HasReplacement => ReplacementId is { IsEmpty: false };

    public Retirement Normalize()
    {
        if (!HasReplacement)
        {
            if (Ratio != 0)
            {
                throw AppError.Invalid("replacement ratio requires a replacement ingredient");
            }

            return this with { ReplacementId = null, Ratio = 0 };
        }

        IngredientId replacementId = ReplacementId!.Value;
        _ = IngredientId.Parse(replacementId.Value);
        double ratio = Ratio == 0 ? 1 : Ratio;
        if (!double.IsFinite(ratio) || ratio <= 0)
        {
            throw AppError.Invalid("replacement ratio must be a finite number greater than zero");
        }

        return this with { ReplacementId = replacementId, Ratio = ratio };
    }

    public void Validate() => _ = Normalize();
}
