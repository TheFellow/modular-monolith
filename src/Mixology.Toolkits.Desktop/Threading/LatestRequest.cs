namespace Mixology.Toolkits.Desktop.Threading;

public readonly record struct LatestResult<T>(bool IsCurrent, T? Value)
{
    internal static LatestResult<T> Current(T value) => new(true, value);

    internal static LatestResult<T> Superseded() => new(false, default);
}

/// <summary>
/// Gives each asynchronous request a generation, cancels the previous generation, and prevents
/// late responses from being published. Disposal cancels and observes every accepted request.
/// </summary>
public sealed class LatestRequest<T> : IAsyncDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<long, CancellationTokenSource> cancellations = [];
    private readonly Dictionary<Task, CompletionRegistration> pending = [];
    private long generation;
    private bool disposed;

    public Task<LatestResult<T>> RunAsync(
        Func<CancellationToken, Task<T>> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CancellationTokenSource cancellation;
        long acceptedGeneration;
        Task<LatestResult<T>> task;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            foreach (CancellationTokenSource prior in cancellations.Values.ToArray())
            {
                prior.Cancel();
            }

            acceptedGeneration = ++generation;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellations.Add(acceptedGeneration, cancellation);
            task = ExecuteAsync(acceptedGeneration, cancellation, request);
            pending.Add(
                task,
                new CompletionRegistration(acceptedGeneration, cancellation));
            _ = task.ContinueWith(
                static (completed, owner) => ((LatestRequest<T>)owner!).Complete(completed),
                this,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        return task;
    }

    public async ValueTask DisposeAsync()
    {
        Task[] drain;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            _ = ++generation;
            foreach (CancellationTokenSource cancellation in cancellations.Values.ToArray())
            {
                cancellation.Cancel();
            }

            drain = pending.Keys.Select(ObserveAsync).ToArray();
        }

        await Task.WhenAll(drain).ConfigureAwait(false);
    }

    private async Task<LatestResult<T>> ExecuteAsync(
        long acceptedGeneration,
        CancellationTokenSource cancellation,
        Func<CancellationToken, Task<T>> request)
    {
        T value = await request(cancellation.Token).ConfigureAwait(false);
        lock (sync)
        {
            return !disposed && acceptedGeneration == generation
                ? LatestResult<T>.Current(value)
                : LatestResult<T>.Superseded();
        }
    }

    private void Complete(Task<LatestResult<T>> completed)
    {
        if (completed.IsFaulted)
        {
            // Preserve the faulted task for its caller while preventing an ignored request from
            // becoming an unobserved process-level exception after it leaves the pending set.
            _ = completed.Exception;
        }

        CompletionRegistration registration;
        lock (sync)
        {
            if (!pending.Remove(completed, out registration!))
            {
                return;
            }

            _ = cancellations.Remove(registration.Generation);
        }

        registration.Cancellation.Dispose();
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The caller owns request errors. Disposal only guarantees observation and draining.
        }
    }

    private sealed record CompletionRegistration(
        long Generation,
        CancellationTokenSource Cancellation);
}
