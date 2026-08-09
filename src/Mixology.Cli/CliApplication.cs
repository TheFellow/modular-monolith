using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Dispatcher;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Audit.Models;
using Mixology.Modules.Audit.Requests;
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
using Mixology.Persistence;

namespace Mixology.Cli;

public static class CliApplication
{
    public static RootCommand Build(
        TextWriter? output = null,
        TextWriter? error = null,
        TextReader? input = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;
        input ??= Console.In;
        RootCommand root = new("Manage the Mixology modular-monolith application.");
        Option<string> database = new("--db")
        {
            Description = "Path to the device-local Mixology SQLite database.",
            DefaultValueFactory = _ => Path.GetFullPath("mixology.db"),
        };
        Option<string> actor = new("--actor", "--as")
        {
            Description = "Actor identity: owner, manager, sommelier, bartender, or anonymous.",
            DefaultValueFactory = _ => "owner",
        };
        root.Options.Add(database);
        root.Options.Add(actor);

        IngredientsCommandContext ingredientsContext = new(
            async (parseResult, cancellationToken) => await HostedIngredientsCommandSession.OpenAsync(
                parseResult.GetValue(database) ?? throw AppError.Invalid("database path is required"),
                Actor.Parse(parseResult.GetValue(actor)),
                cancellationToken).ConfigureAwait(false),
            output,
            error);
        AuditCommandContext auditContext = new(
            async (parseResult, cancellationToken) => await HostedAuditCommandSession.OpenAsync(
                parseResult.GetValue(database) ?? throw AppError.Invalid("database path is required"),
                Actor.Parse(parseResult.GetValue(actor)),
                cancellationToken).ConfigureAwait(false),
            output,
            error);
        DrinksCommandContext drinksContext = new(
            async (parseResult, cancellationToken) => await HostedDrinksCommandSession.OpenAsync(
                parseResult.GetValue(database) ?? throw AppError.Invalid("database path is required"),
                Actor.Parse(parseResult.GetValue(actor)),
                cancellationToken).ConfigureAwait(false),
            input,
            output,
            error);
        InventoryCommandContext inventoryContext = new(
            async (parseResult, cancellationToken) => await HostedInventoryCommandSession.OpenAsync(
                parseResult.GetValue(database) ?? throw AppError.Invalid("database path is required"),
                Actor.Parse(parseResult.GetValue(actor)),
                cancellationToken).ConfigureAwait(false),
            output,
            error);

        Command status = new("status", "Initialize storage and report foundation readiness.");
        status.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                string databasePath = parseResult.GetValue(database)
                    ?? throw AppError.Invalid("database path is required");
                _ = Actor.Parse(parseResult.GetValue(actor));
                using IHost host = await OpenHostAsync(databasePath, cancellationToken).ConfigureAwait(false);
                await output.WriteLineAsync("Mixology foundation is ready.").ConfigureAwait(false);
                return 0;
            }
            catch (Exception exception)
            {
                return await CliErrorAdapter.WriteAsync(error, exception).ConfigureAwait(false);
            }
        });

        root.Subcommands.Add(status);
        root.Subcommands.Add(DrinksCommands.Build(drinksContext));
        root.Subcommands.Add(IngredientsCommands.Build(ingredientsContext));
        root.Subcommands.Add(InventoryCommands.Build(inventoryContext));
        root.Subcommands.Add(AuditCommands.Build(auditContext));
        return root;
    }

    private static async Task<IHost> OpenHostAsync(string databasePath, CancellationToken cancellationToken)
    {
        HostApplicationBuilder builder = MixologyHost.CreateBuilder([]);
        builder.Services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
        builder.AddMixology(databasePath, typeof(MigrationAssemblyMarker).Assembly);
        builder.Services.AddAuditModule();
        builder.Services.AddIngredientsModule();
        builder.Services.AddDrinksModule();
        builder.Services.AddInventoryModule();
        builder.Services.AddMenusModule();
        IHost host = builder.Build();
        try
        {
            await host.Services.GetRequiredService<MixologyStore>()
                .InitializeAsync(cancellationToken)
                .ConfigureAwait(false);
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    private sealed class HostedIngredientsCommandSession(
        IHost host,
        IngredientsModule ingredients,
        MixologySession session) : IIngredientsCommandSession
    {
        public static async ValueTask<IIngredientsCommandSession> OpenAsync(
            string databasePath,
            Actor actor,
            CancellationToken cancellationToken)
        {
            IHost host = await OpenHostAsync(databasePath, cancellationToken).ConfigureAwait(false);
            return new HostedIngredientsCommandSession(
                host,
                host.Services.GetRequiredService<IngredientsModule>(),
                host.Services.GetRequiredService<MixologySessionFactory>().Create(actor));
        }

        public Task<Page<Ingredient>> ListAsync(
            ListIngredientsRequest request,
            CancellationToken cancellationToken) =>
            ingredients.ListAsync(session, request, cancellationToken);

        public Task<Ingredient> GetAsync(IngredientId id, CancellationToken cancellationToken) =>
            ingredients.GetAsync(session, id, cancellationToken);

        public Task<Ingredient> CreateAsync(
            CreateIngredientRequest request,
            CancellationToken cancellationToken) =>
            ingredients.CreateAsync(session, request, cancellationToken);

        public Task<Ingredient> UpdateAsync(
            UpdateIngredientRequest request,
            CancellationToken cancellationToken) =>
            ingredients.UpdateAsync(session, request, cancellationToken);

        public Task<Ingredient> RetireAsync(
            RetireIngredientRequest request,
            CancellationToken cancellationToken) =>
            ingredients.RetireAsync(session, request, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
        }
    }

    private sealed class HostedAuditCommandSession(
        IHost host,
        AuditModule audit,
        MixologySession session) : IAuditCommandSession
    {
        public static async ValueTask<IAuditCommandSession> OpenAsync(
            string databasePath,
            Actor actor,
            CancellationToken cancellationToken)
        {
            IHost host = await OpenHostAsync(databasePath, cancellationToken).ConfigureAwait(false);
            return new HostedAuditCommandSession(
                host,
                host.Services.GetRequiredService<AuditModule>(),
                host.Services.GetRequiredService<MixologySessionFactory>().Create(actor));
        }

        public Task<Page<AuditEntry>> ListAsync(
            ListAuditEntriesRequest request,
            CancellationToken cancellationToken) =>
            audit.ListAsync(session, request, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
        }
    }

    private sealed class HostedDrinksCommandSession(
        IHost host,
        DrinksModule drinks,
        MixologySession session) : IDrinksCommandSession
    {
        public static async ValueTask<IDrinksCommandSession> OpenAsync(
            string databasePath,
            Actor actor,
            CancellationToken cancellationToken)
        {
            IHost host = await OpenHostAsync(databasePath, cancellationToken).ConfigureAwait(false);
            return new HostedDrinksCommandSession(
                host,
                host.Services.GetRequiredService<DrinksModule>(),
                host.Services.GetRequiredService<MixologySessionFactory>().Create(actor));
        }

        public Task<Page<Drink>> ListAsync(ListDrinksRequest request, CancellationToken cancellationToken) =>
            drinks.ListAsync(session, request, cancellationToken);

        public Task<Drink> GetAsync(DrinkId id, CancellationToken cancellationToken) =>
            drinks.GetAsync(session, id, cancellationToken);

        public Task<Drink> CreateAsync(CreateDrinkRequest request, CancellationToken cancellationToken) =>
            drinks.CreateAsync(session, request, cancellationToken);

        public Task<Drink> UpdateAsync(UpdateDrinkRequest request, CancellationToken cancellationToken) =>
            drinks.UpdateAsync(session, request, cancellationToken);

        public Task<Drink> DeleteAsync(DrinkId id, CancellationToken cancellationToken) =>
            drinks.DeleteAsync(session, id, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
        }
    }

    private sealed class HostedInventoryCommandSession(
        IHost host,
        InventoryModule inventory,
        MixologySession session) : IInventoryCommandSession
    {
        public static async ValueTask<IInventoryCommandSession> OpenAsync(
            string databasePath,
            Actor actor,
            CancellationToken cancellationToken)
        {
            IHost host = await OpenHostAsync(databasePath, cancellationToken).ConfigureAwait(false);
            return new HostedInventoryCommandSession(
                host,
                host.Services.GetRequiredService<InventoryModule>(),
                host.Services.GetRequiredService<MixologySessionFactory>().Create(actor));
        }

        public Task<Page<InventoryStock>> ListAsync(
            ListInventoryRequest request,
            CancellationToken cancellationToken) =>
            inventory.ListAsync(session, request, cancellationToken);

        public Task<InventoryStock> GetAsync(
            IngredientId ingredientId,
            CancellationToken cancellationToken) =>
            inventory.GetAsync(session, ingredientId, cancellationToken);

        public Task<InventoryStock> AdjustAsync(
            AdjustInventoryRequest request,
            CancellationToken cancellationToken) =>
            inventory.AdjustAsync(session, request, cancellationToken);

        public Task<InventoryStock> SetAsync(
            SetInventoryRequest request,
            CancellationToken cancellationToken) =>
            inventory.SetAsync(session, request, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
        }
    }
}

public static class CliErrorAdapter
{
    public static async Task<int> WriteAsync(TextWriter error, Exception exception)
    {
        AppError? applicationError = AppError.Find(exception);
        if (applicationError is not null)
        {
            await error.WriteLineAsync(applicationError.UserMessage).ConfigureAwait(false);
            return applicationError.Spec.CliExitCode;
        }

        if (AppError.IsCancellation(exception))
        {
            await error.WriteLineAsync("operation cancelled").ConfigureAwait(false);
            return ErrorCatalog.ExitGeneral;
        }

        await error.WriteLineAsync("internal error").ConfigureAwait(false);
        return ErrorCatalog.ExitGeneral;
    }
}
