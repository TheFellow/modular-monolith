using System.Globalization;
using System.Text;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Ingredients.Models;

namespace Mixology.Tui.Workspaces.Drinks;

public sealed record IngredientOption(IngredientId Id, string Name);

public sealed record RecipeIngredientDraft(
    IngredientId IngredientId,
    string Amount,
    string Unit,
    bool Optional,
    IReadOnlyList<IngredientId> Substitutes);

/// <summary>A toolkit-neutral structured recipe editor state used by the Drinks workspace.</summary>
public sealed class DrinkRecipeEditor : IAsyncDisposable
{
    private readonly object sync = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<RecipeIngredientDraft> ingredients;
    private readonly List<string> steps;
    private IReadOnlyList<IngredientOption> catalog = [];
    private CancellationTokenSource? loadCancellation;
    private Task loadTask = Task.CompletedTask;
    private long generation;
    private bool disposed;

    public DrinkRecipeEditor(Recipe? initial = null)
    {
        ingredients = initial?.Ingredients.Select(static value => new RecipeIngredientDraft(
            value.IngredientId,
            value.Amount.Value.ToString("G", CultureInfo.InvariantCulture),
            value.Amount.Unit.Value,
            value.Optional,
            value.Substitutes.ToArray())).ToList()
            ?? [new(default, string.Empty, Unit.Ounce.Value, false, [])];
        steps = initial?.Steps.ToList() ?? [string.Empty];
        Garnish = initial?.Garnish ?? string.Empty;
    }

    public IReadOnlyList<RecipeIngredientDraft> Ingredients
    {
        get { lock (sync) { return ingredients.ToArray(); } }
    }

    public IReadOnlyList<string> Steps
    {
        get { lock (sync) { return steps.ToArray(); } }
    }

    public IReadOnlyList<IngredientOption> Catalog
    {
        get { lock (sync) { return catalog; } }
    }

    public string Garnish { get; private set; }
    public bool Loading { get; private set; }
    public Exception? LoadError { get; private set; }
    public event Action? Changed;

    public Task LoadCatalogAsync(
        Func<CancellationToken, Task<IReadOnlyList<Ingredient>>> loader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loader);
        CancellationTokenSource source;
        long current;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            loadCancellation?.Cancel();
            source = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
            loadCancellation = source;
            current = ++generation;
            Loading = true;
            LoadError = null;
            loadTask = LoadCoreAsync(loader, source, current);
        }

        Changed?.Invoke();
        return loadTask;
    }

    public void SetIngredient(
        int index,
        IngredientId ingredientId,
        string amount,
        string unit,
        bool optional,
        IEnumerable<IngredientId>? substitutes = null)
    {
        lock (sync)
        {
            RequireIndex(index, ingredients.Count, "ingredient");
            ingredients[index] = new RecipeIngredientDraft(
                ingredientId,
                amount ?? string.Empty,
                unit ?? string.Empty,
                optional,
                substitutes?.ToArray() ?? []);
        }

        Changed?.Invoke();
    }

    public IReadOnlyList<IngredientOption> SearchCatalog(string query, int limit = 5)
    {
        if (limit <= 0)
        {
            throw AppError.Invalid("catalog result limit must be greater than zero");
        }

        string value = query?.Trim() ?? string.Empty;
        lock (sync)
        {
            return catalog.Where(option =>
                    value.Length == 0
                    || option.Name.Contains(value, StringComparison.OrdinalIgnoreCase)
                    || option.Id.Value.Contains(value, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToArray();
        }
    }

    public void SelectIngredient(int index, IngredientId ingredientId)
    {
        lock (sync)
        {
            RequireIndex(index, ingredients.Count, "ingredient");
            if (!catalog.Any(value => value.Id == ingredientId))
            {
                throw AppError.Invalid($"ingredient {ingredientId} is not in the loaded catalog");
            }

            RecipeIngredientDraft current = ingredients[index];
            ingredients[index] = current with
            {
                IngredientId = ingredientId,
                Substitutes = current.Substitutes.Where(value => value != ingredientId).ToArray(),
            };
        }

        Changed?.Invoke();
    }

    public void ToggleOptional(int index)
    {
        lock (sync)
        {
            RequireIndex(index, ingredients.Count, "ingredient");
            RecipeIngredientDraft current = ingredients[index];
            ingredients[index] = current with { Optional = !current.Optional };
        }

        Changed?.Invoke();
    }

    public void ToggleSubstitute(int index, IngredientId ingredientId)
    {
        lock (sync)
        {
            RequireIndex(index, ingredients.Count, "ingredient");
            RecipeIngredientDraft current = ingredients[index];
            if (ingredientId == current.IngredientId)
            {
                throw AppError.Invalid("an ingredient cannot substitute itself");
            }

            List<IngredientId> values = current.Substitutes.ToList();
            if (!values.Remove(ingredientId))
            {
                values.Add(ingredientId);
            }

            ingredients[index] = current with { Substitutes = values };
        }

        Changed?.Invoke();
    }

    public void AddIngredient()
    {
        lock (sync) { ingredients.Add(new(default, string.Empty, Unit.Ounce.Value, false, [])); }
        Changed?.Invoke();
    }

    public void RemoveIngredient(int index)
    {
        lock (sync)
        {
            RequireIndex(index, ingredients.Count, "ingredient");
            if (ingredients.Count == 1)
            {
                throw AppError.Invalid("recipe must have at least 1 ingredient");
            }

            ingredients.RemoveAt(index);
        }

        Changed?.Invoke();
    }

    public void SetStep(int index, string value)
    {
        lock (sync)
        {
            RequireIndex(index, steps.Count, "step");
            steps[index] = value ?? string.Empty;
        }

        Changed?.Invoke();
    }

    public void AddStep()
    {
        lock (sync) { steps.Add(string.Empty); }
        Changed?.Invoke();
    }

    public void RemoveStep(int index)
    {
        lock (sync)
        {
            RequireIndex(index, steps.Count, "step");
            if (steps.Count == 1)
            {
                throw AppError.Invalid("recipe must have at least 1 step");
            }

            steps.RemoveAt(index);
        }

        Changed?.Invoke();
    }

    public void SetGarnish(string value)
    {
        Garnish = value ?? string.Empty;
        Changed?.Invoke();
    }

    public Recipe Build()
    {
        lock (sync)
        {
            RecipeIngredient[] values = ingredients.Select((draft, index) =>
            {
                if (draft.IngredientId.IsEmpty)
                {
                    throw AppError.Invalid($"recipe ingredient {index}: ingredient id is required");
                }

                if (!double.TryParse(
                    draft.Amount,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double amount))
                {
                    throw AppError.Invalid($"recipe ingredient {index}: amount must be a number");
                }

                Unit unit = Unit.Parse(draft.Unit);
                IngredientId[] substitutes = draft.Substitutes.Distinct().ToArray();
                if (substitutes.Contains(draft.IngredientId))
                {
                    throw AppError.Invalid($"recipe ingredient {index}: cannot substitute itself");
                }

                return new RecipeIngredient(
                    draft.IngredientId,
                    Amount.Create(amount, unit),
                    draft.Optional,
                    substitutes);
            }).ToArray();
            return new Recipe(values, steps, Garnish).Normalize();
        }
    }

    public string Render()
    {
        lock (sync)
        {
            StringBuilder output = new("Recipe\n");
            if (Loading)
            {
                _ = output.AppendLine("  Loading ingredient catalog...");
            }
            else if (LoadError is not null)
            {
                _ = output.Append("  Catalog error: ").AppendLine(SafeMessage(LoadError));
            }

            for (int index = 0; index < ingredients.Count; index++)
            {
                RecipeIngredientDraft row = ingredients[index];
                string name = catalog.FirstOrDefault(value => value.Id == row.IngredientId)?.Name
                    ?? (row.IngredientId.IsEmpty ? "(choose ingredient)" : row.IngredientId.Value);
                string substitutes = row.Substitutes.Count == 0
                    ? "none"
                    : string.Join(", ", row.Substitutes.Select(IdName));
                _ = output.Append("  ").Append(index + 1).Append(". ").Append(name)
                    .Append(" · ").Append(row.Amount).Append(' ').Append(row.Unit)
                    .Append(row.Optional ? " · optional" : " · required").AppendLine()
                    .Append("     substitutes: ").AppendLine(substitutes);
            }

            _ = output.AppendLine("  Steps:");
            for (int index = 0; index < steps.Count; index++)
            {
                _ = output.Append("    ").Append(index + 1).Append(". ").AppendLine(steps[index]);
            }

            _ = output.Append("  Garnish: ").Append(string.IsNullOrWhiteSpace(Garnish) ? "(none)" : Garnish);
            return output.ToString();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task pending;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            _ = ++generation;
            lifetime.Cancel();
            loadCancellation?.Cancel();
            pending = loadTask;
        }

        try { await pending.ConfigureAwait(false); }
        catch (Exception exception) when (AppError.IsCancellation(exception)) { }
        loadCancellation?.Dispose();
        lifetime.Dispose();
    }

    private async Task LoadCoreAsync(
        Func<CancellationToken, Task<IReadOnlyList<Ingredient>>> loader,
        CancellationTokenSource source,
        long current)
    {
        try
        {
            IReadOnlyList<Ingredient> loaded = await loader(source.Token).ConfigureAwait(false);
            lock (sync)
            {
                if (disposed || generation != current)
                {
                    return;
                }

                catalog = loaded.Select(static value => new IngredientOption(value.Id, value.Name)).ToArray();
                Loading = false;
            }
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            lock (sync) { if (!disposed && generation == current) { Loading = false; } }
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                if (!disposed && generation == current)
                {
                    LoadError = AppError.Find(exception) is not null
                        ? exception
                        : AppError.Internal("load recipe ingredient catalog", exception);
                    Loading = false;
                }
            }
        }

        Changed?.Invoke();
    }

    private string IdName(IngredientId id) => catalog.FirstOrDefault(value => value.Id == id)?.Name ?? id.Value;

    private static string SafeMessage(Exception exception) =>
        AppError.Find(exception)?.UserMessage ?? "internal error";

    private static void RequireIndex(int index, int count, string kind)
    {
        if (index < 0 || index >= count)
        {
            throw AppError.Invalid($"{kind} index is out of range");
        }
    }
}
