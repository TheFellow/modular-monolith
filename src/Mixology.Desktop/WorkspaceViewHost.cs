using Microsoft.Maui.Controls;
using Mixology.Desktop.Workspaces;
using Mixology.Desktop.Workspaces.Audit;
using Mixology.Desktop.Workspaces.Dashboard;
using Mixology.Desktop.Workspaces.Drinks;
using Mixology.Desktop.Workspaces.Ingredients;
using Mixology.Desktop.Workspaces.Inventory;
using Mixology.Desktop.Workspaces.Menus;
using Mixology.Desktop.Workspaces.Orders;
using Mixology.Desktop.Workspaces.Tags;

namespace Mixology.Desktop;

public sealed class WorkspaceViewHost : ContentView
{
    public static readonly BindableProperty WorkspaceProperty = BindableProperty.Create(
        nameof(Workspace),
        typeof(IDesktopWorkspace),
        typeof(WorkspaceViewHost),
        propertyChanged: OnWorkspaceChanged);

    public IDesktopWorkspace? Workspace
    {
        get => (IDesktopWorkspace?)GetValue(WorkspaceProperty);
        set => SetValue(WorkspaceProperty, value);
    }

    private static void OnWorkspaceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        WorkspaceViewHost host = (WorkspaceViewHost)bindable;
        host.Content = newValue switch
        {
            DashboardViewModel => Bind(new DashboardView(), newValue),
            DrinksWorkspaceViewModel => Bind(new DrinksWorkspaceView(), newValue),
            IngredientsViewModel => Bind(new IngredientsView(), newValue),
            InventoryViewModel => Bind(new InventoryView(), newValue),
            MenusViewModel => Bind(new MenusView(), newValue),
            OrdersViewModel => Bind(new OrdersView(), newValue),
            AuditViewModel => Bind(new AuditView(), newValue),
            TagsViewModel => Bind(new TagsView(), newValue),
            null => null,
            _ => throw new InvalidOperationException($"Unsupported desktop workspace {newValue.GetType().FullName}."),
        };
    }

    private static View Bind(View view, object bindingContext)
    {
        view.BindingContext = bindingContext;
        return view;
    }
}
