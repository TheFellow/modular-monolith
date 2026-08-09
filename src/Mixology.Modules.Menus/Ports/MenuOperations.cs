using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Money;
using Mixology.Kernel.Quality;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Queries;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Queries;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Queries;
using Mixology.Modules.Menus.Models;
using Mixology.Persistence;

namespace Mixology.Modules.Menus.Ports;

internal sealed class MenuOperations(
    DrinkQueries drinks,
    IngredientQueries ingredients,
    InventoryQueries inventory) : IMenuOperations
{
    private const double LowStockServingThreshold = 3d;

    public async ValueTask<MenuDrink> GetDrinkAsync(
        StoreSession session,
        DrinkId id,
        CancellationToken cancellationToken = default)
    {
        Drink drink = await drinks.GetAsync(session, id, cancellationToken).ConfigureAwait(false);
        return new MenuDrink(drink.Id, drink.Name);
    }

    public async ValueTask<Availability> GetAvailabilityAsync(
        StoreSession session,
        DrinkId id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return (await CalculateDetailAsync(session, id, cancellationToken).ConfigureAwait(false)).Status;
        }
        catch (Exception exception) when (!AppError.IsCancellation(exception))
        {
            return Availability.Unavailable;
        }
    }

    public async ValueTask<ReadinessReport> GetReadinessAsync(
        StoreSession session,
        Menu menu,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(menu);
        List<ReadinessFinding> findings = [];
        foreach (MenuItem item in menu.Items)
        {
            Drink drink = await drinks.GetAsync(session, item.DrinkId, cancellationToken).ConfigureAwait(false);
            if (drink.Status == DrinkStatus.ReviewRequired)
            {
                findings.Add(new ReadinessFinding(
                    ReadinessSeverity.Blocker,
                    ReadinessCode.ReviewRequiredDrink,
                    drink.Id,
                    null,
                    $"drink {drink.Id} requires recipe review"));
            }

            AvailabilityDetail detail = await CalculateDetailAsync(
                session,
                drink.Id,
                cancellationToken).ConfigureAwait(false);
            foreach (MissingIngredient missing in detail.Missing)
            {
                try
                {
                    _ = await ingredients.GetAsync(
                        session,
                        missing.IngredientId,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    AppError.IsNotFound(exception) && !AppError.IsCancellation(exception))
                {
                    findings.Add(new ReadinessFinding(
                        ReadinessSeverity.Blocker,
                        ReadinessCode.RetiredOrMissingIngredient,
                        drink.Id,
                        missing.IngredientId,
                        $"drink {drink.Id} references retired or missing ingredient {missing.IngredientId}"));
                }
            }

            foreach (IngredientPick substitution in detail.Substitutions)
            {
                findings.Add(new ReadinessFinding(
                    ReadinessSeverity.Blocker,
                    ReadinessCode.TemporarySubstitution,
                    drink.Id,
                    substitution.OriginalIngredientId,
                    $"drink {drink.Id} relies on temporary substitution {substitution.IngredientId} for {substitution.OriginalIngredientId}"));
            }

            if (detail.Status == Availability.Unavailable)
            {
                findings.Add(new ReadinessFinding(
                    ReadinessSeverity.Blocker,
                    ReadinessCode.Unavailable,
                    drink.Id,
                    null,
                    $"drink {drink.Id} is unavailable"));
            }
            else if (detail.Status == Availability.Limited && detail.Substitutions.Count == 0)
            {
                findings.Add(new ReadinessFinding(
                    ReadinessSeverity.Warning,
                    ReadinessCode.LowStock,
                    drink.Id,
                    null,
                    $"drink {drink.Id} has low stock"));
            }
        }

        return new ReadinessReport(menu.Id, menu.Status, findings);
    }

    public async ValueTask<MenuAnalysis> AnalyzeAsync(
        StoreSession session,
        Menu menu,
        double targetMargin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(menu);
        if (targetMargin is <= 0d or >= 1d)
        {
            throw AppError.Invalid("target margin must be between 0 and 1");
        }

        List<MenuItemAnalysis> analyzed = [];
        int availableCount = 0;
        double marginTotal = 0d;
        int marginCount = 0;
        foreach (MenuItem item in menu.Items)
        {
            string name = item.DisplayName ?? string.Empty;
            if (name.Length == 0)
            {
                try
                {
                    name = (await drinks.GetAsync(
                        session,
                        item.DrinkId,
                        cancellationToken).ConfigureAwait(false)).Name;
                }
                catch (Exception exception) when (!AppError.IsCancellation(exception))
                {
                }
            }

            AvailabilityDetail detail;
            try
            {
                detail = await CalculateDetailAsync(session, item.DrinkId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (!AppError.IsCancellation(exception))
            {
                detail = AvailabilityDetail.Unavailable;
            }

            if (detail.Status != Availability.Unavailable)
            {
                availableCount++;
            }

            DrinkCost cost;
            try
            {
                cost = await CalculateCostAsync(
                    session,
                    item.DrinkId,
                    targetMargin,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (!AppError.IsCancellation(exception))
            {
                cost = DrinkCost.UnknownCost;
            }

            double? margin = Margin(item.Price, cost);
            if (margin is { } value)
            {
                marginTotal += value;
                marginCount++;
            }

            analyzed.Add(new MenuItemAnalysis(
                item.DrinkId,
                name,
                detail.Status,
                detail.Substitutions.Select(static pick => new AppliedSubstitution(
                    pick.OriginalIngredientId,
                    pick.IngredientId,
                    pick.Ratio,
                    pick.QualityImpact)).ToArray(),
                cost.Total,
                cost.Unknown,
                item.Price,
                margin,
                cost.Suggested));
        }

        analyzed.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        return new MenuAnalysis(
            menu,
            analyzed,
            availableCount,
            menu.Items.Count,
            marginCount == 0 ? null : marginTotal / marginCount);
    }

    public async ValueTask<IReadOnlyList<IngredientFulfillment>?> FulfillIngredientsAsync(
        StoreSession session,
        IReadOnlyList<RecipeIngredient> requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(requirements);
        (IReadOnlyList<IngredientPick> picks, bool fulfilled) = await PlanAsync(
            session,
            requirements.ToArray(),
            cancellationToken).ConfigureAwait(false);
        return fulfilled
            ? picks.Select(static pick => new IngredientFulfillment(
                pick.IngredientId,
                pick.Required,
                pick.Available,
                pick.UsedSubstitution,
                pick.Ratio,
                pick.QualityImpact)).ToArray()
            : null;
    }

    private async Task<AvailabilityDetail> CalculateDetailAsync(
        StoreSession session,
        DrinkId drinkId,
        CancellationToken cancellationToken)
    {
        Drink drink = await drinks.GetAsync(session, drinkId, cancellationToken).ConfigureAwait(false);
        RecipeIngredient[] requirements = drink.Recipe.Ingredients
            .Where(static requirement => !requirement.Optional)
            .ToArray();
        (IReadOnlyList<IngredientPick> picks, bool fulfilled) = await PlanAsync(
            session,
            requirements,
            cancellationToken).ConfigureAwait(false);
        if (!fulfilled)
        {
            List<MissingIngredient> missing = [];
            foreach (RecipeIngredient requirement in requirements)
            {
                bool hasSubstitute = requirement.Substitutes.Count != 0 ||
                    await HasCatalogSubstituteAsync(
                        session,
                        requirement.IngredientId,
                        cancellationToken).ConfigureAwait(false);
                missing.Add(new MissingIngredient(requirement.IngredientId, hasSubstitute));
            }

            return new AvailabilityDetail(Availability.Unavailable, missing, []);
        }

        bool limited = drink.Status == DrinkStatus.ReviewRequired;
        IngredientPick[] substitutions = picks.Where(static pick => pick.UsedSubstitution).ToArray();
        foreach (IngredientPick pick in picks)
        {
            Amount threshold = pick.Required.Multiply(LowStockServingThreshold);
            if (pick.Available.LessThan(threshold))
            {
                limited = true;
            }
        }

        if (substitutions.Length != 0)
        {
            limited = true;
        }

        return new AvailabilityDetail(
            limited ? Availability.Limited : Availability.Available,
            [],
            substitutions);
    }

    private async Task<(IReadOnlyList<IngredientPick> Picks, bool Fulfilled)> PlanAsync(
        StoreSession session,
        RecipeIngredient[] requirements,
        CancellationToken cancellationToken)
    {
        List<IReadOnlyList<IngredientPick>> candidateSets = [];
        foreach (RecipeIngredient requirement in requirements)
        {
            IReadOnlyList<IngredientPick> candidates = await AvailableCandidatesAsync(
                session,
                requirement,
                cancellationToken).ConfigureAwait(false);
            if (candidates.Count == 0)
            {
                return ([], false);
            }

            candidateSets.Add(candidates);
        }

        IngredientPick[] selected = new IngredientPick[requirements.Length];
        Dictionary<IngredientId, Amount> reserved = [];
        bool Assign(int index)
        {
            if (index == candidateSets.Count)
            {
                return true;
            }

            foreach (IngredientPick pick in candidateSets[index])
            {
                Amount total = pick.Required;
                bool hadPrior = reserved.TryGetValue(pick.IngredientId, out Amount? prior);
                if (hadPrior)
                {
                    try
                    {
                        total = prior!.Convert(pick.Required.Unit).Add(pick.Required);
                    }
                    catch (InvalidError)
                    {
                        continue;
                    }
                }

                Amount available;
                try
                {
                    available = pick.Available.Convert(total.Unit);
                }
                catch (InvalidError)
                {
                    continue;
                }

                if (available.Value < total.Value)
                {
                    continue;
                }

                selected[index] = pick;
                reserved[pick.IngredientId] = total;
                if (Assign(index + 1))
                {
                    return true;
                }

                if (hadPrior)
                {
                    reserved[pick.IngredientId] = prior!;
                }
                else
                {
                    reserved.Remove(pick.IngredientId);
                }
            }

            return false;
        }

        return Assign(0) ? (selected, true) : ([], false);
    }

    private async Task<IReadOnlyList<IngredientPick>> AvailableCandidatesAsync(
        StoreSession session,
        RecipeIngredient requirement,
        CancellationToken cancellationToken)
    {
        List<Candidate> candidates =
        [
            new(requirement.IngredientId, requirement.Amount, true, 1d, Quality.Equivalent),
        ];
        HashSet<IngredientId> seen = [requirement.IngredientId];
        IReadOnlyList<SubstitutionRule> rules;
        try
        {
            rules = await ingredients.SubstitutionsForAsync(
                session,
                requirement.IngredientId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.IsNotFound(exception) && !AppError.IsCancellation(exception))
        {
            rules = [];
        }

        SubstitutionRule[] orderedRules = rules
            .OrderByDescending(static rule => rule.QualityImpact.Rank)
            .ThenBy(static rule => rule.SubstituteId.Value, StringComparer.Ordinal)
            .ToArray();
        Dictionary<IngredientId, SubstitutionRule> rulesBySubstitute = orderedRules
            .ToDictionary(static rule => rule.SubstituteId);
        foreach (IngredientId substitute in requirement.Substitutes)
        {
            if (rulesBySubstitute.TryGetValue(substitute, out SubstitutionRule? rule))
            {
                Add(rule.SubstituteId, rule.Ratio, rule.QualityImpact);
            }
            else
            {
                Add(substitute, 1d, Quality.Similar);
            }
        }

        foreach (SubstitutionRule rule in orderedRules)
        {
            Add(rule.SubstituteId, rule.Ratio, rule.QualityImpact);
        }

        List<IngredientPick> picks = [];
        foreach (Candidate candidate in candidates)
        {
            InventoryStock stock;
            try
            {
                stock = await inventory.GetAsync(
                    session,
                    candidate.Id,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                AppError.IsNotFound(exception) && !AppError.IsCancellation(exception))
            {
                continue;
            }

            Amount available = stock.Available.Convert(candidate.Required.Unit);
            if (available.Value < candidate.Required.Value)
            {
                continue;
            }

            picks.Add(new IngredientPick(
                requirement.IngredientId,
                candidate.Id,
                candidate.Required,
                available,
                !candidate.Original,
                candidate.Ratio,
                candidate.QualityImpact));
        }

        return picks
            .OrderBy(static pick => pick.UsedSubstitution)
            .ThenByDescending(static pick => pick.QualityImpact.Rank)
            .ThenByDescending(static pick => pick.Available.Value)
            .ThenBy(static pick => pick.IngredientId.Value, StringComparer.Ordinal)
            .ToArray();

        void Add(IngredientId id, double ratio, Quality quality)
        {
            if (seen.Add(id))
            {
                candidates.Add(new Candidate(id, requirement.Amount.Multiply(ratio), false, ratio, quality));
            }
        }
    }

    private async Task<bool> HasCatalogSubstituteAsync(
        StoreSession session,
        IngredientId ingredientId,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await ingredients.SubstitutionsForAsync(
                session,
                ingredientId,
                cancellationToken).ConfigureAwait(false)).Count != 0;
        }
        catch (Exception exception) when (!AppError.IsCancellation(exception))
        {
            return false;
        }
    }

    private async Task<DrinkCost> CalculateCostAsync(
        StoreSession session,
        DrinkId drinkId,
        double targetMargin,
        CancellationToken cancellationToken)
    {
        Drink drink = await drinks.GetAsync(session, drinkId, cancellationToken).ConfigureAwait(false);
        Price? total = null;
        bool unknown = false;
        foreach (RecipeIngredient requirement in drink.Recipe.Ingredients.Where(static value => !value.Optional))
        {
            (IReadOnlyList<IngredientPick> picks, bool fulfilled) = await PlanAsync(
                session,
                [requirement],
                cancellationToken).ConfigureAwait(false);
            if (!fulfilled)
            {
                throw AppError.Invalid(
                    $"missing required ingredient {requirement.IngredientId} for drink {drinkId}");
            }

            IngredientPick pick = picks[0];
            InventoryStock stock = await inventory.GetAsync(
                session,
                pick.IngredientId,
                cancellationToken).ConfigureAwait(false);
            if (stock.UnitCost is not { } unitCost)
            {
                unknown = true;
                continue;
            }

            Amount required = pick.Required.Convert(stock.OnHand.Unit);
            decimal quantity;
            try
            {
                quantity = checked((decimal)required.Value);
            }
            catch (OverflowException exception)
            {
                throw AppError.Invalid($"invalid required quantity {required.Value}", exception);
            }

            Price ingredientCost = unitCost.Multiply(quantity);
            total = total is null ? ingredientCost : total.Value.Add(ingredientCost);
        }

        if (total is null)
        {
            unknown = true;
        }

        Price? suggested = total is { } known && !unknown
            ? known.SuggestedPrice(targetMargin)
            : null;
        return new DrinkCost(total, suggested, unknown);
    }

    private static double? Margin(Price? menuPrice, DrinkCost cost)
    {
        if (menuPrice is not { IsZero: false } price ||
            cost.Total is not { } ingredientCost ||
            cost.Unknown ||
            price.Currency != ingredientCost.Currency)
        {
            return null;
        }

        return (double)((price.Amount - ingredientCost.Amount) / price.Amount);
    }

    private sealed record Candidate(
        IngredientId Id,
        Amount Required,
        bool Original,
        double Ratio,
        Quality QualityImpact);

    private sealed record IngredientPick(
        IngredientId OriginalIngredientId,
        IngredientId IngredientId,
        Amount Required,
        Amount Available,
        bool UsedSubstitution,
        double Ratio,
        Quality QualityImpact);

    private sealed record MissingIngredient(IngredientId IngredientId, bool HasSubstitute);

    private sealed record AvailabilityDetail(
        Availability Status,
        IReadOnlyList<MissingIngredient> Missing,
        IReadOnlyList<IngredientPick> Substitutions)
    {
        public static AvailabilityDetail Unavailable { get; } = new(Availability.Unavailable, [], []);
    }

    private readonly record struct DrinkCost(Price? Total, Price? Suggested, bool Unknown)
    {
        public static DrinkCost UnknownCost { get; } = new(null, null, true);
    }
}
