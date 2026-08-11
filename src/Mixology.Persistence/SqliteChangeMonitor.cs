using System.Threading.Channels;
using Microsoft.Data.Sqlite;

namespace Mixology.Persistence;

public interface IStoreChangeSource
{
    ChannelReader<long> Changes { get; }
}

public sealed class SqliteChangeMonitor : IStoreChangeSource, IAsyncDisposable
{
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly string connectionString;
    private readonly TimeSpan pollInterval;
    private readonly TimeProvider timeProvider;
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<long> changes = Channel.CreateBounded<long>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = false,
        SingleWriter = true,
    });
    private readonly Task run;
    private long epoch;
    private int disposed;

    internal SqliteChangeMonitor(StoreSettings settings, TimeProvider timeProvider, TimeSpan pollInterval)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);

        SqliteConnectionStringBuilder builder = new(settings.ConnectionString) { Pooling = false };
        connectionString = builder.ToString();
        this.timeProvider = timeProvider;
        this.pollInterval = pollInterval;
        run = RunAsync(lifetime.Token);
    }

    public ChannelReader<long> Changes => changes.Reader;

    public long Epoch => Interlocked.Read(ref epoch);

    public Task Ready => ready.Task;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        lifetime.Cancel();
        try
        {
            await run.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lifetime.Dispose();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            long? version = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await using SqliteConnection connection = new(connectionString);
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    long baseline = await ReadVersionAsync(connection, cancellationToken).ConfigureAwait(false);
                    _ = ready.TrySetResult();
                    if (version.HasValue)
                    {
                        Publish();
                    }

                    version = baseline;
                    using PeriodicTimer timer = new(pollInterval, timeProvider);
                    while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                    {
                        long current = await ReadVersionAsync(connection, cancellationToken).ConfigureAwait(false);
                        if (current != version)
                        {
                            version = current;
                            Publish();
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SqliteException)
                {
                    await Task.Delay(pollInterval, timeProvider, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _ = ready.TrySetCanceled(cancellationToken);
            changes.Writer.TryComplete();
        }
    }

    private void Publish()
    {
        long next = Interlocked.Increment(ref epoch);
        _ = changes.Writer.TryWrite(next);
    }

    private static async Task<long> ReadVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA data_version;";
        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
