using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Ingredients.Tests;

public sealed class IngredientsModuleTests
{
    [Fact]
    public async Task CrudPersistsThroughTheRealPipelineAndRestartShapedStore()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);

        Ingredient created = await fixture.Module.CreateAsync(
            manager,
            new CreateIngredientRequest("  Gin  ", IngredientCategory.Spirit, Unit.Ounce, "  Juniper  "));
        Ingredient loaded = await fixture.Module.GetAsync(fixture.Session(Actor.Anonymous), created.Id);
        Ingredient updated = await fixture.Module.UpdateAsync(
            manager,
            new UpdateIngredientRequest(created.Id, Description: "  London dry  "));

        Assert.StartsWith("ing-", created.Id.Value, StringComparison.Ordinal);
        Assert.Equal("Gin", loaded.Name);
        Assert.Equal("Juniper", loaded.Description);
        Assert.Equal("Gin", updated.Name);
        Assert.Equal("London dry", updated.Description);
        Assert.Equal(2, await fixture.ScalarAsync<long>("SELECT COUNT(*) FROM audit_entries"));
    }

    [Fact]
    public async Task AuthorizationDenialAndUniqueConflictRemainPreciselyTyped()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        CreateIngredientRequest request = new("Gin", IngredientCategory.Spirit, Unit.Ounce);

        await Assert.ThrowsAsync<PermissionError>(() => fixture.Module.CreateAsync(
            fixture.Session(Actor.Bartender),
            request));
        await fixture.Module.CreateAsync(fixture.Session(Actor.Manager), request);
        ConflictError conflict = await Assert.ThrowsAsync<ConflictError>(() => fixture.Module.CreateAsync(
            fixture.Session(Actor.Manager),
            request));

        Assert.Contains("unique value", conflict.Message, StringComparison.Ordinal);
        Assert.Equal(1, await fixture.ScalarAsync<long>("SELECT COUNT(*) FROM ingredients"));
        Assert.Equal(3, await fixture.ScalarAsync<long>("SELECT COUNT(*) FROM audit_entries"));
    }

    [Fact]
    public async Task ListUsesExactFiltersAndStablePermissionAwareCursorPages()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        await fixture.Module.CreateAsync(
            manager,
            new CreateIngredientRequest("Vodka", IngredientCategory.Spirit, Unit.Ounce));
        await fixture.Module.CreateAsync(
            manager,
            new CreateIngredientRequest("Gin", IngredientCategory.Spirit, Unit.Ounce));
        await fixture.Module.CreateAsync(
            manager,
            new CreateIngredientRequest("Lime", IngredientCategory.Juice, Unit.Ounce));

        ListIngredientsRequest request = new(Filter: "category == \"spirit\"", Limit: 1);
        Mixology.Kernel.Paging.Page<Ingredient> first = await fixture.Module.ListAsync(fixture.Session(Actor.Anonymous), request);
        Mixology.Kernel.Paging.Page<Ingredient> second = await fixture.Module.ListAsync(
            fixture.Session(Actor.Anonymous),
            request with { Cursor = first.Next });

        Assert.Single(first.Items);
        Assert.False(first.Next.IsEmpty);
        Assert.Single(second.Items);
        Assert.True(second.Next.IsEmpty);
        Assert.Equal(2, await fixture.Module.CountAsync(fixture.Session(Actor.Anonymous), request));
        Assert.All(first.Items.Concat(second.Items), item => Assert.Equal(IngredientCategory.Spirit, item.Category));
    }

    [Fact]
    public async Task RetirementValidatesReplacementAndSoftDeletesTheSource()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        MixologySession manager = fixture.Session(Actor.Manager);
        Ingredient gin = await fixture.Module.CreateAsync(
            manager,
            new CreateIngredientRequest("Gin", IngredientCategory.Spirit, Unit.Ounce));
        Ingredient vodka = await fixture.Module.CreateAsync(
            manager,
            new CreateIngredientRequest("Vodka", IngredientCategory.Spirit, Unit.Milliliter));
        Ingredient lime = await fixture.Module.CreateAsync(
            manager,
            new CreateIngredientRequest("Lime", IngredientCategory.Juice, Unit.Ounce));

        await Assert.ThrowsAsync<InvalidError>(() => fixture.Module.RetireAsync(
            manager,
            new RetireIngredientRequest(gin.Id, new Retirement(lime.Id))));
        Ingredient retired = await fixture.Module.RetireAsync(
            manager,
            new RetireIngredientRequest(gin.Id, new Retirement(vodka.Id, 0.75)));

        Assert.Equal(fixture.Now, retired.DeletedAt);
        await Assert.ThrowsAsync<NotFoundError>(() => fixture.Module.GetAsync(manager, gin.Id));
        Assert.DoesNotContain(gin.Id, await fixture.Module.ActiveIdsAsync(manager, [gin.Id, gin.Id, vodka.Id]));
        Assert.Contains(vodka.Id, await fixture.Module.ActiveIdsAsync(manager, [gin.Id, gin.Id, vodka.Id]));
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly ServiceProvider services;

        private Fixture(string root, ServiceProvider services, DateTimeOffset now)
        {
            this.root = root;
            this.services = services;
            Now = now;
            Module = services.GetRequiredService<IngredientsModule>();
        }

        public DateTimeOffset Now { get; }
        public IngredientsModule Module { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-ingredients-tests", Guid.NewGuid().ToString("N"));
            string databasePath = Path.Combine(root, "mixology.db");
            DateTimeOffset now = new(2026, 8, 9, 20, 30, 0, TimeSpan.Zero);
            ServiceCollection collection = new();
            collection.AddLogging();
            collection.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            collection.AddMixologyPersistence(databasePath, typeof(MigrationAssemblyMarker).Assembly);
            collection.AddMixologyApplication();
            collection.AddAuditModule();
            collection.AddIngredientsModule();
            ServiceProvider services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            Fixture fixture = new(root, services, now);
            await services.GetRequiredService<MixologyStore>().InitializeAsync();
            return fixture;
        }

        public MixologySession Session(Actor actor) =>
            services.GetRequiredService<MixologySessionFactory>().Create(actor);

        public async Task<T> ScalarAsync<T>(string commandText)
        {
            await using StoreSession session = await services.GetRequiredService<MixologyStore>().OpenSessionAsync();
            await session.Context.Database.OpenConnectionAsync();
            await using System.Data.Common.DbCommand command = session.Context.Database.GetDbConnection().CreateCommand();
            command.CommandText = commandText;
            object value = await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("scalar returned null");
            return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
