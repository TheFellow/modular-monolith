using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Money;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Tagging;

namespace Mixology.Seed;

public sealed class SeedRunner(
    MixologySessionFactory sessions,
    IngredientsModule ingredients,
    InventoryModule inventory,
    DrinksModule drinks,
    MenusModule menus,
    TaggingModule tagging)
{
    public async Task<SeedResult> RunAsync(
        SeedDataset dataset,
        TextWriter output,
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(databasePath);
        MixologySession session = sessions.Create(Actor.Owner);

        Dictionary<string, IngredientId> ingredientIds = new(StringComparer.Ordinal);
        await output.WriteLineAsync("Creating ingredients...").ConfigureAwait(false);
        foreach (SeedIngredient value in dataset.Ingredients)
        {
            Ingredient created;
            try
            {
                created = await ingredients.CreateAsync(
                    session,
                    new CreateIngredientRequest(
                        value.Name,
                        IngredientCategory.Parse(value.Category),
                        Unit.Parse(value.Unit),
                        value.Description),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw Contextualize($"create ingredient \"{value.Name}\"", exception);
            }

            ingredientIds[value.Key] = created.Id;
            await ReplaceTagsAsync(
                session,
                created.EntityUid,
                value.Tags,
                $"tag ingredient \"{value.Name}\"",
                cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync($"  {created.Name}: {created.Id}").ConfigureAwait(false);
        }

        await output.WriteLineAsync($"  Created {dataset.Ingredients.Count} ingredients").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("Setting inventory levels...").ConfigureAwait(false);
        foreach (SeedIngredient value in dataset.Ingredients)
        {
            InventoryStock stock;
            try
            {
                stock = await inventory.SetAsync(
                    session,
                    new SetInventoryRequest(
                        ingredientIds[value.Key],
                        Amount.Create(value.Stock.Quantity, Unit.Parse(value.Unit)),
                        Price.Parse(value.Stock.Cost)),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw Contextualize($"set inventory for \"{value.Name}\"", exception);
            }

            await ReplaceTagsAsync(
                session,
                stock.EntityUid,
                value.Stock.Tags,
                $"tag inventory for \"{value.Name}\"",
                cancellationToken).ConfigureAwait(false);
        }

        await output.WriteLineAsync("  Inventory stocked").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("Creating drinks...").ConfigureAwait(false);
        List<DrinkId> drinkIds = new(dataset.Drinks.Count);
        foreach (SeedDrink value in dataset.Drinks)
        {
            List<RecipeIngredient> recipeIngredients = new(value.Recipe.Ingredients.Count);
            foreach (SeedRecipeIngredient recipeValue in value.Recipe.Ingredients)
            {
                if (!ingredientIds.TryGetValue(recipeValue.Key, out IngredientId ingredientId))
                {
                    throw AppError.Invalid(
                        $"unknown ingredient key \"{recipeValue.Key}\" in drink \"{value.Name}\"");
                }

                try
                {
                    recipeIngredients.Add(new RecipeIngredient(
                        ingredientId,
                        Amount.Create(recipeValue.Amount, Unit.Parse(recipeValue.Unit))));
                }
                catch (Exception exception)
                {
                    throw Contextualize(
                        $"parse amount for ingredient \"{recipeValue.Key}\" in drink \"{value.Name}\"",
                        exception);
                }
            }

            Drink created;
            try
            {
                created = await drinks.CreateAsync(
                    session,
                    new CreateDrinkRequest(
                        value.Name,
                        DrinkCategory.Parse(value.Category),
                        GlassType.Parse(value.Glass),
                        new Recipe(recipeIngredients, value.Recipe.Steps, value.Recipe.Garnish),
                        value.Description),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw Contextualize($"create drink \"{value.Name}\"", exception);
            }

            drinkIds.Add(created.Id);
            await ReplaceTagsAsync(
                session,
                created.EntityUid,
                value.Tags,
                $"tag drink \"{value.Name}\"",
                cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync($"  {created.Name}: {created.Id}").ConfigureAwait(false);
        }

        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("Creating menu...").ConfigureAwait(false);
        Menu menu;
        try
        {
            menu = await menus.CreateAsync(
                session,
                new CreateMenuRequest("Classic Cocktails"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw Contextualize("create menu", exception);
        }

        await ReplaceTagsAsync(
            session,
            menu.EntityUid,
            ["collection=classics", "service=all-day"],
            "tag menu",
            cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync($"  Menu: {menu.Id}").ConfigureAwait(false);
        foreach (DrinkId drinkId in drinkIds)
        {
            try
            {
                _ = await menus.AddDrinkAsync(
                    session,
                    new AddMenuItemRequest(menu.Id, drinkId),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw Contextualize("add drink to menu", exception);
            }
        }

        try
        {
            menu = await menus.PublishAsync(session, menu.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw Contextualize("publish menu", exception);
        }

        await output.WriteLineAsync($"  Menu published with {drinkIds.Count} drinks").ConfigureAwait(false);
        await WriteSummaryAsync(output, dataset, menu, databasePath).ConfigureAwait(false);
        return new SeedResult(dataset.Ingredients.Count, drinkIds.Count, menu);
    }

    private async Task ReplaceTagsAsync(
        MixologySession session,
        EntityUid target,
        IReadOnlyList<string>? values,
        string context,
        CancellationToken cancellationToken)
    {
        if (values is not { Count: > 0 })
        {
            return;
        }

        try
        {
            _ = await tagging.ReplaceAsync(
                session,
                target,
                new TagCollection(values.Select(Tag.Parse)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw Contextualize(context, exception);
        }
    }

    private static async Task WriteSummaryAsync(
        TextWriter output,
        SeedDataset dataset,
        Menu menu,
        string databasePath)
    {
        string databaseOption = $"--db \"{databasePath.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("=== Seed Complete ===").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("Created:").ConfigureAwait(false);
        await output.WriteLineAsync($"  - {dataset.Ingredients.Count} ingredients").ConfigureAwait(false);
        await output.WriteLineAsync($"  - {dataset.Drinks.Count} classic cocktails").ConfigureAwait(false);
        await output.WriteLineAsync("  - 1 published menu").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("View the menu with cost analysis:").ConfigureAwait(false);
        await output.WriteLineAsync(
            $"  mixology {databaseOption} menus show --id {menu.Id} --costs --target-margin 0.7").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("List all drinks:").ConfigureAwait(false);
        await output.WriteLineAsync($"  mixology {databaseOption} drinks list").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("Check inventory:").ConfigureAwait(false);
        await output.WriteLineAsync($"  mixology {databaseOption} inventory list").ConfigureAwait(false);
    }

    private static Exception Contextualize(string context, Exception exception)
    {
        if (AppError.IsCancellation(exception))
        {
            return exception;
        }

        AppError? error = AppError.Find(exception);
        string detail = $"{context}: {exception.Message}";
        return error?.Kind switch
        {
            ErrorKind.Invalid => AppError.Invalid(detail, exception),
            ErrorKind.NotFound => AppError.NotFound(detail, exception),
            ErrorKind.Permission => AppError.Permission(detail, exception),
            ErrorKind.Conflict => AppError.Conflict(detail, exception),
            ErrorKind.FailedPrecondition => AppError.FailedPrecondition(detail, exception),
            ErrorKind.Internal => AppError.Internal(detail, exception),
            _ => AppError.Internal(context, exception),
        };
    }
}

public sealed record SeedResult(int IngredientCount, int DrinkCount, Menu Menu);
