using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Presentation.Dashboard;
using Xunit;

namespace Mixology.Gui.Tests;

public sealed class DesktopHostTests
{
    [Fact]
    public async Task HostMigratesStoreAndLoadsDashboardThroughRealModules()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "mixology-desktop-host",
            Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "mixology.db");
        try
        {
            await using DesktopHost host = await DesktopHost.OpenAsync(
                DesktopOptions.Create(database, "owner"),
                TestContext.Current.CancellationToken);
            MixologySession session = host.Services.GetRequiredService<MixologySessionFactory>()
                .Create(Actor.Owner);

            DashboardResult result = await host.Services.GetRequiredService<DashboardService>()
                .LoadAsync(session, TestContext.Current.CancellationToken);

            Assert.False(result.IsPartial);
            Assert.Equal(0, result.Data.DrinkCount);
            Assert.Equal(0, result.Data.IngredientCount);
            Assert.Equal(0, result.Data.InventoryCount);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
