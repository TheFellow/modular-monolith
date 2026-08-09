namespace Mixology.Toolkits.Tui;

public delegate ValueTask TuiCommand(TuiCommandContext context, CancellationToken cancellationToken);

public sealed class TuiCommandQueue
{
    public const int DefaultDrainLimit = 10_000;

    private readonly Queue<TuiCommand> commands = new();
    private readonly TuiCommandContext context;
    private bool draining;

    public TuiCommandQueue() => context = new TuiCommandContext(commands);

    public int PendingCount => commands.Count;

    public void Enqueue(TuiCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        commands.Enqueue(command);
    }

    public async Task<int> DrainAsync(
        int limit = DefaultDrainLimit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "drain limit must be greater than zero");
        }

        if (draining)
        {
            throw new TuiLifecycleException("command queue is already draining");
        }

        draining = true;
        int drained = 0;
        try
        {
            while (commands.Count != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (drained == limit)
                {
                    throw new CommandDrainLimitExceededException(limit);
                }

                TuiCommand command = commands.Dequeue();
                drained++;
                await command(context, cancellationToken).ConfigureAwait(false);
            }

            return drained;
        }
        finally
        {
            draining = false;
        }
    }
}

public sealed class TuiCommandContext
{
    private readonly Queue<TuiCommand> commands;

    internal TuiCommandContext(Queue<TuiCommand> commands) => this.commands = commands;

    public void Enqueue(TuiCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        commands.Enqueue(command);
    }
}

public sealed class CommandDrainLimitExceededException(int limit)
    : InvalidOperationException($"terminal command drain exceeded {limit} commands")
{
    public int Limit { get; } = limit;
}
