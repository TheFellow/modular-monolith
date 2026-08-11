using Microsoft.EntityFrameworkCore;
using Mixology.Persistence.Model;

namespace Mixology.Persistence;

public sealed class MixologyStore(
    IDbContextFactory<MixologyDbContext> contextFactory,
    StoreSettings settings,
    TimeProvider timeProvider)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(settings.DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using MixologyDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", cancellationToken).ConfigureAwait(false);

        if (!await context.Set<StoreMetadataRow>().AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            context.Add(new StoreMetadataRow { Id = 1, CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<StoreSession> OpenSessionAsync(CancellationToken cancellationToken = default) =>
        new(await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false));

    public SqliteChangeMonitor MonitorChanges(TimeSpan? pollInterval = null) =>
        new(settings, timeProvider, pollInterval ?? SqliteChangeMonitor.DefaultPollInterval);
}
