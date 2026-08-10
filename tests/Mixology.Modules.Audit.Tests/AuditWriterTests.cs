using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Inventory;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
using Mixology.Modules.Tagging;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Audit.Tests;

public sealed class AuditWriterTests
{
    [Fact]
    public async Task SuccessfulCommandPersistsOrderedDeduplicatedTouches()
    {
        await using AuditFixture fixture = await AuditFixture.CreateAsync();
        MixologySession session = fixture.CreateSession(Actor.Manager);
        EntityUid ingredient = new(EntityIds.IngredientType, IngredientId.New().Value);
        EntityUid drink = new(EntityIds.DrinkType, DrinkId.New().Value);

        await session.ExecuteAsync(Operation.Command("Ingredient.create"), context =>
        {
            context.Touch(ingredient);
            context.Touch(drink);
            context.Touch(ingredient);
            return Task.CompletedTask;
        });

        Assert.Equal(1, await fixture.ScalarAsync<long>("SELECT COUNT(*) FROM audit_entries"));
        Assert.Equal(2, await fixture.ScalarAsync<long>("SELECT COUNT(*) FROM audit_touches"));
        Assert.Equal("Ingredient.create", await fixture.ScalarAsync<string>("SELECT action FROM audit_entries"));
        Assert.Equal("manager", await fixture.ScalarAsync<string>("SELECT principal_id FROM audit_entries"));
        Assert.Equal(ingredient.Id, await fixture.ScalarAsync<string>(
            "SELECT entity_id FROM audit_touches ORDER BY position LIMIT 1"));
    }

    [Fact]
    public async Task FailedCommandIsPersistedWithItsTypedKind()
    {
        await using AuditFixture fixture = await AuditFixture.CreateAsync();
        MixologySession session = fixture.CreateSession(Actor.Owner);
        ConflictError expected = AppError.Conflict("duplicate ingredient");

        ConflictError actual = await Assert.ThrowsAsync<ConflictError>(() => session.ExecuteAsync(
            Operation.Command("Ingredient.create"),
            _ => throw expected));

        Assert.Same(expected, actual);
        Assert.Equal(0L, await fixture.ScalarAsync<long>("SELECT success FROM audit_entries"));
        Assert.Equal((long)ErrorKind.Conflict, await fixture.ScalarAsync<long>("SELECT error_kind FROM audit_entries"));
        Assert.Equal("duplicate ingredient", await fixture.ScalarAsync<string>("SELECT error FROM audit_entries"));
    }

    private sealed class AuditFixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;
        private readonly MixologyStore store;

        private AuditFixture(string root, ServiceProvider services)
        {
            this.root = root;
            this.services = services;
            store = services.GetRequiredService<MixologyStore>();
        }

        public static async Task<AuditFixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-audit-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddMixologyPersistence(
                Path.Combine(root, "mixology.db"),
                typeof(MigrationAssemblyMarker).Assembly);
            collection.AddMixologyApplication();
            collection.AddAuditModule();
            collection.AddIngredientsModule();
            collection.AddDrinksModule();
            collection.AddInventoryModule();
            collection.AddMenusModule();
            collection.AddOrdersModule();
            collection.AddTaggingModule();
            ServiceProvider services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            AuditFixture fixture = new(root, services);
            await fixture.store.InitializeAsync();
            return fixture;
        }

        public MixologySession CreateSession(Actor actor) =>
            services.GetRequiredService<MixologySessionFactory>().Create(actor);

        public async Task<T> ScalarAsync<T>(string commandText)
        {
            await using StoreSession session = await store.OpenSessionAsync();
            await session.Context.Database.OpenConnectionAsync();
            try
            {
                await using DbCommand command = session.Context.Database.GetDbConnection().CreateCommand();
                command.CommandText = commandText;
                object? value = await command.ExecuteScalarAsync();
                object converted = Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture)
                    ?? throw new InvalidOperationException("Scalar query returned null.");
                return (T)converted;
            }
            finally
            {
                await session.Context.Database.CloseConnectionAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
