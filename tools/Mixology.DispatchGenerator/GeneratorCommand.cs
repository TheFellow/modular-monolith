using System.Text;

namespace Mixology.DispatchGenerator;

public static class GeneratorCommand
{
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            Options options = Parse(args);
            string manifest = File.ReadAllText(options.ManifestPath);
            byte[] generated = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(DispatcherGenerator.Generate(manifest));

            if (options.Check)
            {
                if (!File.Exists(options.OutputPath)
                    || !File.ReadAllBytes(options.OutputPath).AsSpan().SequenceEqual(generated))
                {
                    error.WriteLine($"Generated dispatcher is stale: {options.OutputPath}");
                    return 1;
                }

                output.WriteLine($"Generated dispatcher is current: {options.OutputPath}");
                return 0;
            }

            string? directory = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(options.OutputPath, generated);
            output.WriteLine($"Generated dispatcher: {options.OutputPath}");
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or System.Text.Json.JsonException)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static Options Parse(IReadOnlyList<string> args)
    {
        string? manifest = null;
        string? output = null;
        bool check = false;

        for (int index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--manifest":
                    manifest = ReadValue(args, ref index, "--manifest");
                    break;
                case "--output":
                    output = ReadValue(args, ref index, "--output");
                    break;
                case "--check":
                    check = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        if (string.IsNullOrWhiteSpace(manifest) || string.IsNullOrWhiteSpace(output))
        {
            throw new ArgumentException(
                "Usage: Mixology.DispatchGenerator --manifest <path> --output <path> [--check]");
        }

        return new Options(Path.GetFullPath(manifest), Path.GetFullPath(output), check);
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string option)
    {
        index++;
        if (index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Missing value for {option}.");
        }

        return args[index];
    }

    private sealed record Options(string ManifestPath, string OutputPath, bool Check);
}
