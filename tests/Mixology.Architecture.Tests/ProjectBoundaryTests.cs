using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Inventory;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
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
    public void ModuleDependenciesRemainAcyclicAndIndependentOfExecutables()
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
        }

        Dictionary<string, ProjectInfo> modulesByName = modules.ToDictionary(
            static module => module.Name,
            StringComparer.Ordinal);
        HashSet<string> visiting = new(StringComparer.Ordinal);
        HashSet<string> visited = new(StringComparer.Ordinal);
        foreach (ProjectInfo module in modules)
        {
            Visit(module.Name);
        }

        void Visit(string name)
        {
            if (visited.Contains(name))
            {
                return;
            }

            Assert.True(visiting.Add(name), $"cyclic module dependency through {name}");
            foreach (string dependency in modulesByName[name].MixologyReferences.Where(modulesByName.ContainsKey))
            {
                Visit(dependency);
            }

            _ = visiting.Remove(name);
            _ = visited.Add(name);
        }
    }

    [Fact]
    public void CrossDomainCodeOnlyConsumesOwnerModelsQueriesAndEvents()
    {
        Regex reference = new(
            @"Mixology\.Modules\.(?<owner>[A-Za-z0-9_]+)(?<suffix>(?:\.[A-Za-z0-9_]+)*)",
            RegexOptions.CultureInvariant);

        foreach (ProjectInfo module in Graph.Projects.Where(
                     project => project.Name.StartsWith("Mixology.Modules.", StringComparison.Ordinal)))
        {
            string owner = module.Name["Mixology.Modules.".Length..];
            string directory = Path.GetDirectoryName(module.ProjectPath)!;
            foreach (string file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                         .Where(static file =>
                             !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                             !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            {
                foreach (Match match in reference.Matches(File.ReadAllText(file)))
                {
                    string referencedOwner = match.Groups["owner"].Value;
                    if (string.Equals(owner, referencedOwner, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string suffix = match.Groups["suffix"].Value;
                    Assert.True(
                        suffix.StartsWith(".Models", StringComparison.Ordinal) ||
                        suffix.StartsWith(".Queries", StringComparison.Ordinal) ||
                        suffix.StartsWith(".Events", StringComparison.Ordinal),
                        $"{Path.GetRelativePath(directory, file)} reaches into {referencedOwner}{suffix}; " +
                        "cross-domain code may consume only owner Models, Queries, or Events");
                }
            }
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
            typeof(DrinksModule).Assembly,
            typeof(InventoryModule).Assembly,
            typeof(MenusModule).Assembly,
            typeof(OrdersModule).Assembly,
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
