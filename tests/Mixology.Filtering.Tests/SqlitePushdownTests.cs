using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mixology.Persistence;
using Mixology.Persistence.Model;
using Xunit;

namespace Mixology.Filtering.Tests;

public sealed class SqlitePushdownTests
{
    [Fact]
    public async Task ImpliedPredicateTranslatesThroughEfSqliteAndResidualRemainsExact()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<MixologyDbContext> options = new DbContextOptionsBuilder<MixologyDbContext>()
            .UseSqlite(connection)
            .Options;
        await using MixologyDbContext context = new(options, [new ProbeConfiguration()]);
        await context.Database.EnsureCreatedAsync();
        context.AddRange(
            new ProbeRow { Id = 1, Category = "wine", Price = 8, Tags = "featured" },
            new ProbeRow { Id = 2, Category = "cocktail", Price = 14, Tags = "classic" },
            new ProbeRow { Id = 3, Category = "beer", Price = 7, Tags = "featured" });
        await context.SaveChangesAsync();

        FilterSchema<ProbeView> schema = new(
        [
            Filter.Field("Category", (ProbeView view) => view.Category),
            Filter.Field("Price", (ProbeView view) => view.Price),
            Filter.Field("Tags", (ProbeView view) => view.Tags),
        ]);
        FilterExpression<ProbeView> filter = Filter.Parse(
            schema,
            "(Category == \"wine\" && Tags contains \"featured\") || (Category == \"cocktail\" && Price >= 12)")!;
        FilterPersistenceMap<ProbeRow> map = new(
        [
            Filter.PersistedField("Category", (ProbeRow row) => row.Category),
            Filter.PersistedField("Price", (ProbeRow row) => row.Price),
        ]);

        ProbeRow[] candidates = await context.Set<ProbeRow>().Where(filter.BuildPushdown(map)!).OrderBy(row => row.Id).ToArrayAsync();
        ProbeView[] exact = candidates.Select(row => new ProbeView(row.Category, row.Price, row.Tags.Split(','))).Where(filter.Match).ToArray();

        Assert.Equal([1, 2], candidates.Select(row => row.Id));
        Assert.Equal(["wine", "cocktail"], exact.Select(view => view.Category));
    }

    private sealed record ProbeView(string Category, int Price, string[] Tags);

    private sealed class ProbeRow
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public int Price { get; set; }
        public string Tags { get; set; } = string.Empty;
    }

    private sealed class ProbeConfiguration : IModuleModelConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProbeRow>().HasKey(row => row.Id);
        }
    }
}
