using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Mixology.Desktop.Workspaces;
using Mixology.Desktop.Workspaces.Dashboard;
using Mixology.Presentation.Dashboard;
using Mixology.Presentation.Navigation;
using Xunit;
using DashboardData = Mixology.Presentation.Dashboard.Dashboard;

namespace Mixology.Desktop.Tests;

public sealed class DesktopControlTests
{
    [AvaloniaFact]
    public async Task WindowUsesSemanticDashboardControlsWithoutAdvertisingFutureRoutes()
    {
        DashboardViewModel dashboard = new(_ => Task.FromResult(new DashboardResult(
            new DashboardData(6, 18, 18, 1, 1, 0, 1, 2, 1, 4, []))));
        NavigationProjection projection = new(
            [
                new NavigationItem(NavigationProjector.DashboardWorkspace, "Dashboard"),
                new NavigationItem(NavigationProjector.DrinksWorkspace, "Drinks"),
            ],
            []);
        await using ShellViewModel shell = new(
            projection,
            new Dictionary<WorkspaceId, Func<IDesktopWorkspace>>
            {
                [NavigationProjector.DashboardWorkspace] = () => dashboard,
            });
        await shell.InitializeAsync();
        MainWindow window = new(shell);

        window.Show();
        Button navigation = Assert.Single(
            window.GetVisualDescendants().OfType<Button>(),
            button => Equals(button.Content, "Dashboard"));
        Button refresh = Assert.Single(
            window.GetVisualDescendants().OfType<Button>(),
            button => Equals(button.Content, "Refresh"));
        Assert.NotNull(navigation.Command);
        Assert.NotNull(refresh.Command);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "6");
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), button =>
            Equals(button.Content, "Drinks"));
        window.Close();
    }
}
