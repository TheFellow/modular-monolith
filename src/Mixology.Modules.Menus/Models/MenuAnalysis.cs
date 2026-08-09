using Mixology.Kernel.Entities;
using Mixology.Kernel.Money;
using Mixology.Kernel.Quality;

namespace Mixology.Modules.Menus.Models;

public sealed record AppliedSubstitution(
    IngredientId OriginalIngredientId,
    IngredientId SubstituteIngredientId,
    double Ratio,
    Quality QualityImpact);

public sealed record MenuItemAnalysis(
    DrinkId DrinkId,
    string Name,
    Availability Availability,
    IReadOnlyList<AppliedSubstitution> Substitutions,
    Price? Cost,
    bool CostUnknown,
    Price? MenuPrice,
    double? Margin,
    Price? SuggestedPrice);

public sealed record MenuAnalysis(
    Menu Menu,
    IReadOnlyList<MenuItemAnalysis> Items,
    int AvailableCount,
    int TotalCount,
    double? AverageMargin);

public sealed record MenuDrink(DrinkId Id, string Name);
