using System.Reflection;
using System.Xml.Linq;
using Mixology.Modules.Audit;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Inventory;
using Mixology.Persistence.Model;
using Xunit;

namespace Mixology.Architecture.Tests;

public sealed class ProjectBoundaryTests
{
    private static readonly ProjectGraph Graph = ProjectGraph.Load();

    [Fact]
    public void FoundationProjectsOnlyPointInward()
    {
        ProjectInfo kernel = Graph["Mixology.Kernel"];
        ProjectInfo filtering = Graph["Mixology.Filtering"];
        ProjectInfo persistence = Graph["Mixology.Persistence"];
        ProjectInfo application = Graph["Mixology.Application"];

        Assert.Empty(kernel.MixologyReferences);
        AssertReferencesWithin(filtering, "Mixology.Kernel");
        AssertReferencesWithin(persistence, "Mixology.Kernel", "Mixology.Filtering");
        AssertReferencesWithin(
            application,
            "Mixology.Kernel",
            "Mixology.Filtering",
            "Mixology.Persistence");

        Assert.Contains("Mixology.Kernel", filtering.MixologyReferences);
        Assert.Contains("Mixology.Filtering", persistence.MixologyReferences);
        Assert.Contains("Mixology.Kernel", application.MixologyReferences);
        Assert.Contains("Mixology.Persistence", application.MixologyReferences);
    }

    [Fact]
    public void ModulesRemainIndependentOfExecutablesAndSiblingModules()
    {
        ProjectInfo[] modules = Graph.Projects
            .Where(project => project.Name.StartsWith("Mixology.Modules.", StringComparison.Ordinal))
            .ToArray();
        string[] executables = Graph.Projects
            .Where(project => project.IsExecutable)
            .Select(project => project.Name)
            .ToArray();

        Assert.NotEmpty(modules);
        Assert.NotEmpty(executables);

        foreach (ProjectInfo module in modules)
        {
            Assert.DoesNotContain(module.MixologyReferences, executables.Contains);
            Assert.DoesNotContain(
                module.MixologyReferences,
                reference => reference.StartsWith("Mixology.Modules.", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void CliIsTheCompositionEdge()
    {
        ProjectInfo cli = Graph["Mixology.Cli"];
        Assert.True(cli.IsExecutable);

        foreach (ProjectInfo project in Graph.Projects.Where(project => project.Name != cli.Name))
        {
            Assert.DoesNotContain(cli.Name, project.MixologyReferences);
        }

        string[] requiredCompositionReferences =
        [
            "Mixology.Application",
            "Mixology.Dispatcher",
            "Mixology.Migrations",
            .. Graph.Projects
                .Where(project => project.Name.StartsWith("Mixology.Modules.", StringComparison.Ordinal))
                .Select(project => project.Name),
        ];

        foreach (string required in requiredCompositionReferences)
        {
            Assert.Contains(required, cli.MixologyReferences);
        }
    }

    [Fact]
    public void ModulePersistenceDetailsStayBehindTheirModuleBoundary()
    {
        Assembly[] modules =
        [
            typeof(AuditServiceCollectionExtensions).Assembly,
            typeof(IngredientsModule).Assembly,
            typeof(InventoryModule).Assembly,
        ];

        foreach (Assembly module in modules)
        {
            Type[] persistenceTypes = module.GetTypes()
                .Where(type => type.Namespace?.EndsWith(".Persistence", StringComparison.Ordinal) == true)
                .ToArray();
            Type[] storageDetails = persistenceTypes
                .Where(type =>
                    type.Name.EndsWith("Row", StringComparison.Ordinal) ||
                    type.Name.EndsWith("Repository", StringComparison.Ordinal))
                .ToArray();

            Assert.NotEmpty(storageDetails);
            Assert.All(storageDetails, type => Assert.False(type.IsVisible, $"{type.FullName} is public"));
            Assert.All(
                persistenceTypes.Where(type => type.IsVisible),
                type => Assert.True(
                    typeof(IModuleModelConfiguration).IsAssignableFrom(type),
                    $"{type.FullName} exposes persistence details beyond the model-composition contract"));
        }
    }

    private static void AssertReferencesWithin(ProjectInfo project, params string[] permitted)
    {
        Assert.All(
            project.MixologyReferences,
            reference => Assert.Contains(reference, permitted));
    }

    private sealed record ProjectInfo(
        string Name,
        string ProjectPath,
        bool IsExecutable,
        IReadOnlySet<string> MixologyReferences);

    private sealed class ProjectGraph(IReadOnlyDictionary<string, ProjectInfo> projects)
    {
        public IEnumerable<ProjectInfo> Projects => projects.Values;

        public ProjectInfo this[string name] => projects[name];

        public static ProjectGraph Load()
        {
            string repository = FindRepositoryRoot();
            string source = Path.Combine(repository, "src");
            Dictionary<string, ProjectInfo> projects = Directory
                .EnumerateFiles(source, "Mixology.*.csproj", SearchOption.AllDirectories)
                .Select(ReadProject)
                .ToDictionary(project => project.Name, StringComparer.Ordinal);

            return new ProjectGraph(projects);
        }

        private static ProjectInfo ReadProject(string projectPath)
        {
            XDocument document = XDocument.Load(projectPath, LoadOptions.SetLineInfo);
            string name = Path.GetFileNameWithoutExtension(projectPath);
            bool executable = document
                .Descendants("OutputType")
                .Any(element =>
                    string.Equals(element.Value.Trim(), "Exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(element.Value.Trim(), "WinExe", StringComparison.OrdinalIgnoreCase));
            HashSet<string> references = document
                .Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(include => !string.IsNullOrWhiteSpace(include))
                .Select(include => Path.GetFileNameWithoutExtension(include!))
                .Where(reference => reference.StartsWith("Mixology.", StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal);

            return new ProjectInfo(name, projectPath, executable, references);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Mixology.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                $"Could not locate Mixology.slnx above {AppContext.BaseDirectory}.");
        }
    }
}
