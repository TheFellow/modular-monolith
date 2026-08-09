namespace Mixology.DispatchGenerator;

internal static class Program
{
    public static int Main(string[] args) => GeneratorCommand.Run(args, Console.Out, Console.Error);
}
