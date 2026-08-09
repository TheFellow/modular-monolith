using System.Text;
using Mixology.DispatchGenerator;

namespace Mixology.Dispatcher.Tests;

public sealed class DispatcherGeneratorTests
{
    [Fact]
    public void GenerationIsSortedAndRunsEveryPreparationBeforeHandling()
    {
        const string manifest = """
            {
              "version": 1,
              "namespace": "Example.Generated",
              "className": "ExampleDispatcher",
              "routes": [
                {
                  "event": "Example.Events.ZebraEvent",
                  "handlers": [
                    { "type": "Example.Handlers.ZebraHandler", "prepare": true },
                    { "type": "Example.Handlers.BetaHandler", "prepare": true },
                    { "type": "Example.Handlers.AlphaHandler", "prepare": false }
                  ]
                },
                {
                  "event": "Example.Events.AlphaEvent",
                  "handlers": [
                    { "type": "Example.Handlers.OnlyHandler", "prepare": false }
                  ]
                }
              ]
            }
            """;

        string generated = DispatcherGenerator.Generate(manifest);

        Assert.True(
            generated.IndexOf("Example.Events.AlphaEvent", StringComparison.Ordinal)
                < generated.IndexOf("Example.Events.ZebraEvent", StringComparison.Ordinal));
        Assert.True(
            generated.IndexOf("CreateInstance<global::Example.Handlers.AlphaHandler>", StringComparison.Ordinal)
                < generated.IndexOf("CreateInstance<global::Example.Handlers.ZebraHandler>", StringComparison.Ordinal));
        Assert.True(
            generated.LastIndexOf("CreateInstance<", StringComparison.Ordinal)
                < generated.IndexOf("handler1.PrepareAsync", StringComparison.Ordinal));
        int zebraRouteStart = generated.IndexOf(
            "global::Example.Events.ZebraEvent domainEvent",
            StringComparison.Ordinal);
        string zebraRoute = generated[zebraRouteStart..];
        Assert.True(
            zebraRoute.LastIndexOf(".PrepareAsync", StringComparison.Ordinal)
                < zebraRoute.IndexOf(".HandleAsync", StringComparison.Ordinal));
        Assert.Equal(generated, DispatcherGenerator.Generate(manifest));
        Assert.DoesNotContain('\r', generated);
    }

    [Fact]
    public void DuplicateHandlerIsRejected()
    {
        const string manifest = """
            {
              "version": 1,
              "namespace": "Example.Generated",
              "className": "ExampleDispatcher",
              "routes": [{
                "event": "Example.Events.Created",
                "handlers": [
                  { "type": "Example.Handlers.Consumer", "prepare": false },
                  { "type": "Example.Handlers.Consumer", "prepare": true }
                ]
              }]
            }
            """;

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => DispatcherGenerator.Generate(manifest));

        Assert.Contains("Duplicate handler", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckModeDetectsStaleOutputWithoutRewritingIt()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"mixology-dispatch-generator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string manifestPath = Path.Combine(directory, "routes.json");
            string outputPath = Path.Combine(directory, "Dispatcher.g.cs");
            File.WriteAllText(manifestPath, EmptyManifest, Encoding.UTF8);

            Assert.Equal(0, RunCommand(manifestPath, outputPath, check: false));
            Assert.Equal(0, RunCommand(manifestPath, outputPath, check: true));

            File.AppendAllText(outputPath, "// stale\n", Encoding.UTF8);
            byte[] staleBytes = File.ReadAllBytes(outputPath);

            Assert.Equal(1, RunCommand(manifestPath, outputPath, check: true));
            Assert.Equal(staleBytes, File.ReadAllBytes(outputPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CommittedDispatcherIsCurrent()
    {
        string repository = FindRepositoryRoot();
        string manifestPath = Path.Combine(
            repository, "src", "Mixology.Dispatcher", "dispatcher.routes.json");
        string outputPath = Path.Combine(
            repository, "src", "Mixology.Dispatcher", "Generated", "DomainEventDispatcher.g.cs");

        byte[] expected = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            .GetBytes(DispatcherGenerator.Generate(File.ReadAllText(manifestPath)));

        Assert.Equal(expected, File.ReadAllBytes(outputPath));
    }

    private const string EmptyManifest = """
        {
          "version": 1,
          "namespace": "Example.Generated",
          "className": "ExampleDispatcher",
          "routes": []
        }
        """;

    private static int RunCommand(string manifestPath, string outputPath, bool check)
    {
        List<string> arguments =
        [
            "--manifest", manifestPath,
            "--output", outputPath,
        ];
        if (check)
        {
            arguments.Add("--check");
        }

        return GeneratorCommand.Run(arguments, TextWriter.Null, TextWriter.Null);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Mixology.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
