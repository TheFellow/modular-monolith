using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Presentation;
using Mixology.Modules.Orders.Requests;
using Mixology.Presentation.Mutations;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Desktop.Threading;
using MenuItemModel = Mixology.Modules.Menus.Models.MenuItem;

namespace Mixology.Desktop.Workspaces.Orders;

public enum OrderDesktopMode { Browse, Detail, Place, CompleteConfirmation, CancelConfirmation }

public sealed record OrderRowViewModel(Order Order, string MenuName)
{
    public string Id => Order.Id.Value;
    public string Status => Order.Status.Value;
    public string Created => Order.CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    public string Items => Order.Items.Sum(static item => item.Quantity).ToString(CultureInfo.CurrentCulture);
}

public sealed record OrderMenuOption(Menu Menu)
{
    public MenuId Id => Menu.Id;
    public string Display => $"{Menu.Name} · {Menu.Id.Value}";
}

public sealed record OrderDrinkOption(DrinkId Id, string Name, Availability Availability, string Price)
{
    public string Display => $"{Name} · {Availability.Value} · {Price}";
}

public sealed partial class OrderPlacementLine : ObservableObject
{
    public OrderPlacementLine(OrderDrinkOption drink) => Drink = drink;
    public OrderDrinkOption Drink { get; }
    [ObservableProperty]
    public partial string Quantity { get; set; } = "1";
    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;
}

public sealed record OrderLineViewModel(OrderItem Item, string Name)
{
    public string Display => $"{Item.Quantity} × {Name}" + (Item.Notes.Length == 0 ? string.Empty : $" · {Item.Notes}");
}

public sealed partial class OrdersViewModel : ObservableObject, IDesktopWorkspace
{
    private readonly IOrderDesktopOperations operations;
    private readonly IUiDispatcher dispatcher;
    private readonly LatestRequest<OrderLoadOutcome> loads = new();
    private readonly LatestRequest<OrderMutationOutcome> mutations = new();
    private readonly List<Cursor> history = [];
    private readonly Dictionary<MenuId, string> menuNames = [];
    private readonly Dictionary<DrinkId, string> drinkNames = [];
    private ListOrdersRequest request = new();
    private Cursor cursor;
    private Cursor next;
    private OrderDesktopMode mode;
    private OrderRowViewModel? selected;
    private Order? detail;
    private IReadOnlyList<ActionState> actions = [];
    private bool isDirty;
    private bool disposed;
    private Task active = Task.CompletedTask;

    public OrdersViewModel(IOrderDesktopOperations operations, IUiDispatcher? dispatcher = null)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        this.dispatcher = dispatcher ?? new ImmediateUiDispatcher();
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ApplyFilterCommand = new AsyncRelayCommand(ApplyFilterAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        NextPageCommand = new AsyncRelayCommand(NextPageAsync, CanNext);
        PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync, CanPrevious);
        StartPlaceCommand = new RelayCommand(StartPlace);
        CancelPlaceCommand = new RelayCommand(CancelPlace);
        AddLineCommand = new RelayCommand(AddLine);
        RemoveLineCommand = new RelayCommand<OrderPlacementLine>(RemoveLine);
        PlaceCommand = new AsyncRelayCommand(PlaceAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        BeginCompleteCommand = new RelayCommand(() => Mode = OrderDesktopMode.CompleteConfirmation);
        BeginCancelOrderCommand = new RelayCommand(() => Mode = OrderDesktopMode.CancelConfirmation);
        DismissConfirmationCommand = new RelayCommand(() => Mode = OrderDesktopMode.Detail);
        CompleteCommand = new AsyncRelayCommand(CompleteAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        CancelOrderCommand = new AsyncRelayCommand(CancelOrderAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public static Func<IDesktopWorkspace> CreateFactory(
        OrdersModule orders,
        MenusModule menus,
        DrinksModule drinks,
        OrderActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor,
        IUiDispatcher? dispatcher = null) => () => new OrdersViewModel(
            new ModuleOrderDesktopOperations(orders, menus, drinks, projector, taggedMutations, session, actor), dispatcher);

    public WorkspaceId Id => NavigationProjector.OrdersWorkspace;
    public string Title => "Orders";
    public bool IsDirty => isDirty;
    public bool IsBrowse => Mode == OrderDesktopMode.Browse;
    public bool IsDetail => Mode is OrderDesktopMode.Detail or OrderDesktopMode.CompleteConfirmation or OrderDesktopMode.CancelConfirmation;
    public bool IsPlace => Mode == OrderDesktopMode.Place;
    public bool IsCompleteConfirmation => Mode == OrderDesktopMode.CompleteConfirmation;
    public bool IsCancelConfirmation => Mode == OrderDesktopMode.CancelConfirmation;
    public bool CanPlace => Enabled(OrderActionProjector.PlaceAction);
    public bool CanComplete => Enabled(OrderActionProjector.CompleteAction);
    public bool CanCancel => Enabled(OrderActionProjector.CancelAction);
    public IReadOnlyList<string> Statuses { get; } = ["all", .. OrderStatus.All.Select(static value => value.Value)];
    public string FilterHelp => "Fields: id, menu_id, status, created_at, notes, tags.";
    public ObservableCollection<OrderRowViewModel> Rows { get; } = [];
    public ObservableCollection<OrderMenuOption> Menus { get; } = [];
    public ObservableCollection<OrderDrinkOption> Drinks { get; } = [];
    public ObservableCollection<OrderPlacementLine> PlaceLines { get; } = [];
    public ObservableCollection<OrderLineViewModel> DetailLines { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand ApplyFilterCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public IAsyncRelayCommand PreviousPageCommand { get; }
    public IRelayCommand StartPlaceCommand { get; }
    public IRelayCommand CancelPlaceCommand { get; }
    public IRelayCommand AddLineCommand { get; }
    public IRelayCommand<OrderPlacementLine> RemoveLineCommand { get; }
    public IAsyncRelayCommand PlaceCommand { get; }
    public IRelayCommand BeginCompleteCommand { get; }
    public IRelayCommand BeginCancelOrderCommand { get; }
    public IRelayCommand DismissConfirmationCommand { get; }
    public IAsyncRelayCommand CompleteCommand { get; }
    public IAsyncRelayCommand CancelOrderCommand { get; }
    public Exception? Error { get; private set; }

    public OrderDesktopMode Mode { get => mode; private set { if (SetProperty(ref mode, value)) { OnPropertyChanged(nameof(IsBrowse)); OnPropertyChanged(nameof(IsDetail)); OnPropertyChanged(nameof(IsPlace)); OnPropertyChanged(nameof(IsCompleteConfirmation)); OnPropertyChanged(nameof(IsCancelConfirmation)); } } }
    public OrderRowViewModel? Selected { get => selected; set { if (SetProperty(ref selected, value)) { active = SelectAsync(value); } } }
    public Order? Detail { get => detail; private set => SetProperty(ref detail, value); }

    [ObservableProperty]
    public partial string FilterStatus { get; set; } = "all";
    [ObservableProperty]
    public partial string FilterExpression { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string PageSize { get; set; } = "100";
    [ObservableProperty]
    public partial OrderMenuOption? FilterMenu { get; set; }
    [ObservableProperty]
    public partial OrderMenuOption? PlaceMenu { get; set; }
    [ObservableProperty]
    public partial OrderDrinkOption? PlaceDrink { get; set; }
    [ObservableProperty]
    public partial string PlaceNotes { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string PlaceTags { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool IsLoading { get; set; }
    [ObservableProperty]
    public partial bool IsSubmitting { get; set; }
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    partial void OnPlaceMenuChanged(OrderMenuOption? value) { RebuildDrinks(value); MarkDirty(); }
    partial void OnPlaceNotesChanged(string value) => MarkDirty();
    partial void OnPlaceTagsChanged(string value) => MarkDirty();

    public Task ActivateAsync(CancellationToken cancellationToken = default) => LoadAsync(cursor, cancellationToken);

    public async Task DrainAsync()
    {
        while (true)
        {
            Task snapshot = active; await snapshot.ConfigureAwait(false); if (ReferenceEquals(snapshot, active))
            {
                return;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await loads.DisposeAsync().ConfigureAwait(false); await mutations.DisposeAsync().ConfigureAwait(false);
    }

    private async Task RefreshAsync() => await LoadAsync(cursor, CancellationToken.None).ConfigureAwait(false);

    private async Task ApplyFilterAsync()
    {
        try
        {
            if (!int.TryParse(PageSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit) || limit <= 0)
            {
                throw AppError.Invalid("page size must be greater than zero");
            }

            OrderStatus? status = FilterStatus == "all" ? null : OrderStatus.Parse(FilterStatus);
            request = new(status, FilterMenu?.Id, FilterExpression, Limit: limit);
            cursor = next = default; history.Clear();
            await LoadAsync(default, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (!AppError.IsCancellation(exception)) { PublishError(Safe(exception, "apply order filter")); }
    }

    private async Task NextPageAsync() { if (!CanNext()) { return; } history.Add(cursor); cursor = next; await LoadAsync(cursor, CancellationToken.None).ConfigureAwait(false); }
    private async Task PreviousPageAsync() { if (!CanPrevious()) { return; } int last = history.Count - 1; cursor = history[last]; history.RemoveAt(last); await LoadAsync(cursor, CancellationToken.None).ConfigureAwait(false); }
    private bool CanNext() => !IsLoading && !next.IsEmpty;
    private bool CanPrevious() => !IsLoading && history.Count > 0;

    private async Task LoadAsync(Cursor pageCursor, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await dispatcher.InvokeAsync(() => { IsLoading = true; StatusMessage = "Loading orders…"; NotifyPaging(); }, cancellationToken).ConfigureAwait(false);
        try
        {
            ListOrdersRequest snapshot = request with { Cursor = pageCursor };
            LatestResult<OrderLoadOutcome> latest = await loads.RunAsync(async token =>
            {
                try
                {
                    Task<Page<Order>> pageTask = operations.ListAsync(snapshot, token);
                    Task<OrderCatalog> catalogTask = operations.CatalogAsync(token);
                    Task<IReadOnlyList<ActionState>> actionTask = operations.ProjectAsync(null, token);
                    await Task.WhenAll(pageTask, catalogTask, actionTask).ConfigureAwait(false);
                    return new(await pageTask, await catalogTask, await actionTask, null, null);
                }
                catch (Exception exception) when (AppError.IsCancellation(exception)) { throw; }
                catch (Exception exception) { return new(null, null, [], Safe(exception, "load desktop orders"), null); }
            }, cancellationToken).ConfigureAwait(false);
            if (latest.IsCurrent && latest.Value is not null)
            {
                await dispatcher.InvokeAsync(() => PublishLoad(latest.Value), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (AppError.IsCancellation(exception) && !cancellationToken.IsCancellationRequested) { }
    }

    private void PublishLoad(OrderLoadOutcome outcome)
    {
        if (outcome.Error is not null || outcome.Page is null || outcome.Catalog is null) { PublishError(outcome.Error!); return; }
        string? selectedId = Selected?.Id; actions = outcome.Actions;
        menuNames.Clear(); drinkNames.Clear(); Menus.Clear();
        foreach (Menu menu in outcome.Catalog.Menus.OrderBy(static value => value.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            menuNames[menu.Id] = menu.Name;
            if (menu.Status == MenuStatus.Published)
            {
                Menus.Add(new(menu));
            }
        }
        foreach (var pair in outcome.Catalog.Drinks)
        {
            drinkNames[pair.Key] = pair.Value.Name;
        }

        Rows.Clear(); foreach (Order order in outcome.Page.Items)
        {
            Rows.Add(new(order, menuNames.GetValueOrDefault(order.MenuId, order.MenuId.Value)));
        }

        next = outcome.Page.Next; OrderRowViewModel? keep = Rows.FirstOrDefault(row => row.Id == selectedId); if (!ReferenceEquals(Selected, keep))
        {
            Selected = keep;
        }

        Error = null; IsLoading = false; StatusMessage = $"{Rows.Count} orders"; NotifyActions(); NotifyPaging();
    }

    private async Task SelectAsync(OrderRowViewModel? row)
    {
        if (row is null) { Detail = null; DetailLines.Clear(); Mode = OrderDesktopMode.Browse; return; }
        await RunDetailAsync(async token =>
        {
            Order order = await operations.GetAsync(row.Order.Id, token).ConfigureAwait(false);
            return OrderMutationOutcome.Success(order, await operations.ProjectAsync(order, token).ConfigureAwait(false));
        }, "load order detail").ConfigureAwait(false);
    }

    private void StartPlace()
    {
        if (!CanPlace)
        {
            return;
        }

        PlaceMenu = null; PlaceDrink = null; PlaceLines.Clear(); PlaceNotes = PlaceTags = string.Empty; SetDirty(false); Mode = OrderDesktopMode.Place;
    }

    private void CancelPlace() { PlaceLines.Clear(); SetDirty(false); Mode = Detail is null ? OrderDesktopMode.Browse : OrderDesktopMode.Detail; }

    private void RebuildDrinks(OrderMenuOption? selectedMenu)
    {
        Drinks.Clear(); PlaceDrink = null;
        if (selectedMenu is null)
        {
            return;
        }

        foreach (MenuItemModel item in selectedMenu.Menu.Items)
        {
            Drinks.Add(new(item.DrinkId, drinkNames.GetValueOrDefault(item.DrinkId, item.DisplayName ?? item.DrinkId.Value), item.Availability, item.Price?.ToString() ?? "price unavailable"));
        }
    }

    private void AddLine()
    {
        if (PlaceDrink is null)
        {
            return;
        }

        OrderPlacementLine line = new(PlaceDrink);
        line.PropertyChanged += OnPlacementLineChanged;
        PlaceLines.Add(line);
        SetDirty(true);
    }

    private void RemoveLine(OrderPlacementLine? line)
    {
        if (line is not null && PlaceLines.Remove(line))
        {
            line.PropertyChanged -= OnPlacementLineChanged;
            SetDirty(true);
        }
    }

    private void OnPlacementLineChanged(object? sender, PropertyChangedEventArgs eventArgs) => MarkDirty();

    private async Task PlaceAsync()
    {
        try
        {
            if (PlaceMenu is null)
            {
                throw AppError.Invalid("menu is required");
            }

            PlaceOrderItem[] lines = PlaceLines.Select(line =>
            {
                if (!int.TryParse(line.Quantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantity))
                {
                    throw AppError.Invalid("quantity must be a whole number");
                }

                return new PlaceOrderItem(line.Drink.Id, quantity, line.Notes);
            }).ToArray();
            TagCollection tags = TagCollection.Parse(PlaceTags);
            PlaceOrderRequest request = new PlaceOrderRequest(PlaceMenu.Id, lines, PlaceNotes).Normalize();
            await RunMutationAsync(token => operations.PlaceAsync(request, tags, token), "place order").ConfigureAwait(false);
        }
        catch (Exception exception) when (!AppError.IsCancellation(exception)) { PublishError(Safe(exception, "place order")); }
    }

    private Task CompleteAsync() => Detail is null ? Task.CompletedTask : RunMutationAsync(token => operations.CompleteAsync(Detail.Id, token), "complete order");
    private Task CancelOrderAsync() => Detail is null ? Task.CompletedTask : RunMutationAsync(token => operations.CancelAsync(Detail.Id, token), "cancel order");

    private async Task RunMutationAsync(Func<CancellationToken, Task<Order>> work, string operation)
    {
        ObjectDisposedException.ThrowIf(disposed, this); IsSubmitting = true; StatusMessage = "Saving order…";
        try
        {
            LatestResult<OrderMutationOutcome> latest = await mutations.RunAsync(async token =>
            {
                try { Order order = await work(token).ConfigureAwait(false); return OrderMutationOutcome.Success(order, await operations.ProjectAsync(order, token).ConfigureAwait(false)); }
                catch (Exception exception) when (AppError.IsCancellation(exception)) { throw; }
                catch (Exception exception) { return OrderMutationOutcome.Failed(Safe(exception, operation)); }
            }).ConfigureAwait(false);
            if (latest.IsCurrent && latest.Value is not null)
            {
                await dispatcher.InvokeAsync(() => PublishMutation(latest.Value)).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (AppError.IsCancellation(exception)) { throw; }
    }

    private async Task RunDetailAsync(Func<CancellationToken, Task<OrderMutationOutcome>> work, string operation)
    {
        IsLoading = true; StatusMessage = "Loading order…";
        try
        {
            LatestResult<OrderLoadOutcome> latest = await loads.RunAsync(async token =>
            {
                try { return new(null, null, [], null, await work(token).ConfigureAwait(false)); }
                catch (Exception exception) when (AppError.IsCancellation(exception)) { throw; }
                catch (Exception exception) { return new(null, null, [], Safe(exception, operation), null); }
            }).ConfigureAwait(false);
            if (latest.IsCurrent && latest.Value is not null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    if (latest.Value.Error is not null)
                    {
                        PublishError(latest.Value.Error);
                    }
                    else
                    {
                        PublishMutation(latest.Value.Detail!);
                    }
                }).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (AppError.IsCancellation(exception)) { }
    }

    private void PublishMutation(OrderMutationOutcome outcome)
    {
        if (outcome.Error is not null) { PublishError(outcome.Error); return; }
        Order order = outcome.Order!; actions = outcome.Actions; Detail = order; DetailLines.Clear();
        foreach (OrderItem item in order.Items)
        {
            DetailLines.Add(new(item, drinkNames.GetValueOrDefault(item.DrinkId, item.DrinkId.Value)));
        }

        OrderRowViewModel row = new(order, menuNames.GetValueOrDefault(order.MenuId, order.MenuId.Value));
        OrderRowViewModel? existing = Rows.FirstOrDefault(value => value.Id == order.Id.Value); if (existing is null)
        {
            Rows.Insert(0, row);
        }
        else
        {
            Rows[Rows.IndexOf(existing)] = row;
        }

        selected = row; OnPropertyChanged(nameof(Selected)); SetDirty(false); Mode = OrderDesktopMode.Detail;
        Error = null; IsLoading = IsSubmitting = false; StatusMessage = "Order ready"; NotifyActions();
    }

    private void MarkDirty()
    {
        if (IsPlace)
        {
            SetDirty(PlaceMenu is not null || PlaceLines.Count > 0 || PlaceNotes.Length > 0 || PlaceTags.Length > 0);
        }
    }
    private void SetDirty(bool value) { if (isDirty == value) { return; } isDirty = value; OnPropertyChanged(nameof(IsDirty)); }
    private bool Enabled(ActionId id) => actions.Any(state => state.Id == id && state.Visible && state.Enabled);
    private static Exception Safe(Exception exception, string operation) => AppError.Find(exception) is not null || AppError.IsCancellation(exception) ? exception : AppError.Internal(operation, exception);
    private void PublishError(Exception exception) { Error = exception; IsLoading = IsSubmitting = false; StatusMessage = AppError.Find(exception)?.UserMessage ?? "internal error"; NotifyPaging(); }
    private void NotifyPaging() { NextPageCommand.NotifyCanExecuteChanged(); PreviousPageCommand.NotifyCanExecuteChanged(); }
    private void NotifyActions() { OnPropertyChanged(nameof(CanPlace)); OnPropertyChanged(nameof(CanComplete)); OnPropertyChanged(nameof(CanCancel)); }

    private sealed record OrderLoadOutcome(Page<Order>? Page, OrderCatalog? Catalog, IReadOnlyList<ActionState> Actions, Exception? Error, OrderMutationOutcome? Detail);
    private sealed record OrderMutationOutcome(Order? Order, IReadOnlyList<ActionState> Actions, Exception? Error)
    {
        public static OrderMutationOutcome Success(Order order, IReadOnlyList<ActionState> actions) => new(order, actions, null);
        public static OrderMutationOutcome Failed(Exception error) => new(null, [], error);
    }
}
