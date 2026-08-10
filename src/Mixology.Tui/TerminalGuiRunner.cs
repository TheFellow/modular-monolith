using Mixology.Kernel.Errors;
using Mixology.Toolkits.Tui;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Mixology.Tui;

public interface ITuiRunner
{
    Task RunAsync(TuiShell shell, CancellationToken cancellationToken = default);
}

public sealed class TerminalGuiRunner(
    Func<TerminalApplicationHost>? createHost = null) : ITuiRunner
{
    private readonly Func<TerminalApplicationHost> createHost = createHost ?? (() => TerminalApplicationHost.Create());

    public async Task RunAsync(TuiShell shell, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shell);
        using TerminalApplicationHost host = createHost();
        using MixologyWindow root = new(host.Application, shell);
        await host.RunAsync(root, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class MixologyWindow : Window
{
    private readonly IApplication application;
    private readonly TuiShell shell;
    private readonly Label content;
    private bool disposed;

    public MixologyWindow(IApplication application, TuiShell shell)
    {
        this.application = application ?? throw new ArgumentNullException(nameof(application));
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        Title = "Mixology";
        content = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        Add(content);
        shell.Changed += OnShellChanged;
        KeyDown += OnKeyDown;
        SubViewsLaidOut += OnSubViewsLaidOut;
        Render();
    }

    public string Render(Viewport viewport) => shell.Render(viewport);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            shell.Changed -= OnShellChanged;
            KeyDown -= OnKeyDown;
            SubViewsLaidOut -= OnSubViewsLaidOut;
        }

        base.Dispose(disposing);
    }

    private void OnKeyDown(object? sender, Key key)
    {
        _ = sender;
        if (key == Key.Esc)
        {
            Run(async () => await shell.BackAsync().ConfigureAwait(false));
            key.Handled = true;
            return;
        }

        char? value = MapInput(key);
        if (value is null)
        {
            return;
        }

        Run(async () =>
        {
            bool handled = await shell.HandleAsync(value.Value).ConfigureAwait(false);
            if (handled && shell.StopRequested)
            {
                application.Invoke(() => application.RequestStop(this));
            }
        });
        key.Handled = true;
    }

    public static char? MapInput(Key key)
    {
        ArgumentNullException.ThrowIfNull(key);
        Key unmodified = key.IsCtrl ? key.NoCtrl : key;
        int rune = unmodified.AsRune.Value;
        if (rune is < char.MinValue or > char.MaxValue)
        {
            return null;
        }

        char value = (char)rune;
        if (!key.IsCtrl)
        {
            return value == '\0' ? null : value;
        }

        char letter = char.ToLowerInvariant(value);
        if (letter == 'c')
        {
            return 'q';
        }

        return letter is >= 'a' and <= 'z'
            ? (char)(letter - 'a' + 1)
            : null;
    }

    private void OnSubViewsLaidOut(object? sender, LayoutEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Render();
    }

    private void OnShellChanged() => application.Invoke(Render);

    private void Render()
    {
        if (disposed)
        {
            return;
        }

        int width = Math.Max(Viewport.Width, 0);
        int height = Math.Max(Viewport.Height, 0);
        content.Text = shell.Render(new Mixology.Toolkits.Tui.Viewport(width, height));
        Title = $"Mixology > {shell.CurrentRoute.Label}";
        SetNeedsDraw();
    }

    private async void Run(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
        }
        catch (Exception exception)
        {
            shell.Report(exception);
        }
    }
}
