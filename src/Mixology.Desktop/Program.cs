namespace Mixology.Desktop;

public static class Program
{
    [STAThread]
    public static int Main(string[] args) =>
        DesktopCommandLine.Build().Parse(args).Invoke();
}
