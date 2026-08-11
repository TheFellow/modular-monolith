using System.Xml.Linq;
using Xunit;

namespace Mixology.Gui.Tests;

public sealed class MauiMarkupTests
{
    private static readonly XNamespace Maui = "http://schemas.microsoft.com/dotnet/2021/maui";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2009/xaml";

    public static TheoryData<string, string> WorkspaceViews => new()
    {
        { "Audit/AuditView.xaml", "AuditViewModel" },
        { "Dashboard/DashboardView.xaml", "DashboardViewModel" },
        { "Drinks/DrinksWorkspaceView.xaml", "DrinksWorkspaceViewModel" },
        { "Ingredients/IngredientsView.xaml", "IngredientsViewModel" },
        { "Inventory/InventoryView.xaml", "InventoryViewModel" },
        { "Menus/MenusView.xaml", "MenusViewModel" },
        { "Orders/OrdersView.xaml", "OrdersViewModel" },
        { "Tags/TagsView.xaml", "TagsViewModel" },
    };

    [Theory]
    [MemberData(nameof(WorkspaceViews))]
    public void WorkspaceUsesMauiCompiledBindingsAndSemanticControls(string relativePath, string viewModel)
    {
        XDocument document = XDocument.Load(ProjectPath("Workspaces", relativePath));
        XElement root = Assert.IsType<XElement>(document.Root);
        string dataType = Assert.IsType<string>((string?)root.Attribute(Xaml + "DataType"));

        Assert.Equal(Maui + "ContentView", root.Name);
        Assert.EndsWith(viewModel, dataType, StringComparison.Ordinal);
        Assert.NotEmpty(root.Descendants(Maui + "Label"));
        Assert.DoesNotContain(root.Descendants(), element =>
            element.Name.NamespaceName.Contains("avalonia", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MainPageHostsNavigationAndTheSelectedWorkspace()
    {
        XDocument document = XDocument.Load(ProjectPath("MainPage.xaml"));

        Assert.NotEmpty(document.Descendants(Maui + "Button"));
        Assert.Single(document.Descendants(), element => element.Name.LocalName == "WorkspaceViewHost");
    }

    [Fact]
    public void DesktopProjectUsesNativeMauiTargetsWithoutALinuxGuiTarget()
    {
        XDocument project = XDocument.Load(ProjectPath("Mixology.Gui.csproj"));
        string[] frameworks = project.Descendants("TargetFrameworks")
            .Select(static element => element.Value)
            .ToArray();
        string[] packages = project.Descendants("PackageReference")
            .Select(static element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToArray();

        Assert.Contains(frameworks, value => value.Contains("net10.0-maccatalyst", StringComparison.Ordinal));
        Assert.Contains(frameworks, value => value.Contains("net10.0-windows", StringComparison.Ordinal));
        Assert.DoesNotContain(frameworks, value => value.Contains("linux", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(packages, value => value.StartsWith("Avalonia", StringComparison.Ordinal));
    }

    private static string ProjectPath(params string[] segments)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Mixology.Gui"));
        return Path.Combine([root, .. segments]);
    }
}
