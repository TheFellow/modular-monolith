using System.Text;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Tags;
using Mixology.Toolkits.Tui;

namespace Mixology.Tui.Workspaces.Shared;

internal sealed class WorkspaceForm
{
    private readonly string[] names;
    private readonly Dictionary<string, string> baseline;

    public WorkspaceForm(IEnumerable<FormField> fields)
    {
        FormField[] values = fields.ToArray();
        Model = new FormModel(values);
        names = values.Select(static field => field.Name).ToArray();
        baseline = values.ToDictionary(static field => field.Name, static field => field.Value, StringComparer.Ordinal);
        Model.BeginEdit();
    }

    public FormModel Model { get; }
    public int FocusedIndex { get; private set; }
    public string FocusedName => names[FocusedIndex];
    public InputOwnership InputOwnership => Model.InputOwnership;

    public string this[string name] => Model[name];

    public bool IsFieldDirty(string name) =>
        !string.Equals(Model[name], baseline[name], StringComparison.Ordinal);

    public void Set(string name, string value) => Model.SetValue(name, value);

    public bool Handle(char key)
    {
        if (Model.Mode != FormMode.Edit)
        {
            return true;
        }

        switch (key)
        {
            case '\t':
                FocusedIndex = (FocusedIndex + 1) % names.Length;
                return true;
            case '\b':
            case '\u007f':
                string current = Model[FocusedName];
                Model.SetValue(FocusedName, current.Length == 0 ? current : current[..^1]);
                return true;
            default:
                if (!char.IsControl(key))
                {
                    Model.SetValue(FocusedName, Model[FocusedName] + key);
                }

                return true;
        }
    }

    public TagCollection? DesiredTags(string name)
    {
        if (!IsFieldDirty(name))
        {
            return null;
        }

        try
        {
            return TagCollection.Parse(Model[name].Trim());
        }
        catch (Exception exception) when (AppError.Find(exception) is not null)
        {
            throw AppError.Invalid($"invalid tags: {AppError.Find(exception)!.UserMessage}", exception);
        }
    }

    public string Render(string title, string footer)
    {
        StringBuilder output = new();
        _ = output.AppendLine(title).AppendLine();
        for (int index = 0; index < names.Length; index++)
        {
            string name = names[index];
            _ = output.Append(index == FocusedIndex ? "> " : "  ")
                .Append(name)
                .Append(": ")
                .AppendLine(Model[name]);
            if (Model.Errors.TryGetValue(name, out string? error))
            {
                _ = output.Append("    ! ").AppendLine(error);
            }
        }

        if (Model.Errors.TryGetValue(string.Empty, out string? submission))
        {
            _ = output.AppendLine().Append("Error: ").AppendLine(submission);
        }

        _ = output.AppendLine().Append(footer);
        return output.ToString();
    }
}

internal sealed class WorkspaceRequestTracker : IAsyncDisposable
{
    private readonly object sync = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<CancellationTokenSource> sources = [];
    private readonly List<Task> tasks = [];
    private bool disposed;

    public CancellationTokenSource Create(CancellationToken cancellationToken)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(
                lifetime.Token,
                cancellationToken);
            sources.Add(source);
            return source;
        }
    }

    public Task Track(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            tasks.Add(task);
        }

        return task;
    }

    public async Task DrainAsync()
    {
        while (true)
        {
            Task[] pending;
            lock (sync)
            {
                pending = tasks.Where(static task => !task.IsCompleted).ToArray();
            }

            if (pending.Length == 0)
            {
                return;
            }

            await Task.WhenAll(pending).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task[] pending;
        CancellationTokenSource[] cancellations;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            lifetime.Cancel();
            pending = tasks.ToArray();
            cancellations = sources.ToArray();
        }

        foreach (CancellationTokenSource source in cancellations)
        {
            source.Cancel();
        }

        await Task.WhenAll(pending).ConfigureAwait(false);
        foreach (CancellationTokenSource source in cancellations)
        {
            source.Dispose();
        }

        lifetime.Dispose();
    }
}

internal static class WorkspaceRender
{
    public static string Fit(string value, Viewport viewport) => string.Join('\n', value
        .Split('\n')
        .Select(line => line.Length <= viewport.Width ? line : line[..viewport.Width])
        .Take(viewport.Height));

    public static string TwoPane(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right,
        int width,
        int gap = 2)
    {
        int leftWidth = Math.Max((width - gap) / 2, 1);
        int rightWidth = Math.Max(width - leftWidth - gap, 1);
        int count = Math.Max(left.Count, right.Count);
        StringBuilder output = new();
        for (int index = 0; index < count; index++)
        {
            string leftLine = index < left.Count ? left[index] : string.Empty;
            string rightLine = index < right.Count ? right[index] : string.Empty;
            _ = output.Append(Clip(leftLine, leftWidth).PadRight(leftWidth))
                .Append(' ', gap)
                .AppendLine(Clip(rightLine, rightWidth));
        }

        return output.ToString().TrimEnd();
    }

    private static string Clip(string value, int width) => value.Length <= width ? value : value[..width];
}
