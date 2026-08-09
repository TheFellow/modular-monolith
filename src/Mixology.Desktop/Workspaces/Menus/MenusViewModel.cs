using System.Collections.ObjectModel;
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
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Presentation;
using Mixology.Modules.Menus.Requests;
using Mixology.Presentation.Mutations;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Desktop.Threading;

namespace Mixology.Desktop.Workspaces.Menus;

public enum MenuDesktopMode { Browse, Detail, Create, Edit, DeleteConfirmation }

public sealed record MenuRowViewModel(Menu Menu)
{
    public string Id => Menu.Id.Value;
    public string Name => Menu.Name;
    public string Status => Menu.Status.Value;
    public string Items => Menu.Items.Count.ToString(CultureInfo.CurrentCulture);
    public string Tags => Menu.Tags.Format();
}

public sealed record MenuDrinkOption(DrinkId Id, string Name)
{
    public string Display => $"{Name} · {Id.Value}";
}

public sealed record MenuItemViewModel(MenuItem Item, string Name)
{
    public string DrinkId => Item.DrinkId.Value;
    public string Availability => Item.Availability.Value;
    public string Price => Item.Price?.ToString() ?? "(not priced)";
    public string Display => $"{Name} · {Availability} · {Price}";
}

public sealed partial class MenusViewModel : ObservableObject, IDesktopWorkspace
{
    private readonly IMenuDesktopOperations operations;
    private readonly IUiDispatcher dispatcher;
    private readonly LatestRequest<MenuLoadOutcome> loads = new();
    private readonly LatestRequest<MenuMutationOutcome> mutations = new();
    private readonly List<Cursor> history = [];
    private readonly Dictionary<DrinkId, string> drinkNames = [];
    private Cursor cursor;
    private Cursor next;
    private ListMenusRequest request = new();
    private MenuDesktopMode mode;
    private MenuRowViewModel? selected;
    private Menu? detail;
    private bool disposed;
    private bool isDirty;
    private string baseline = string.Empty;
    private Task active = Task.CompletedTask;
    private IReadOnlyList<ActionState> actions = [];

    public MenusViewModel(IMenuDesktopOperations operations, IUiDispatcher? dispatcher = null)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        this.dispatcher = dispatcher ?? new ImmediateUiDispatcher();
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ApplyFilterCommand = new AsyncRelayCommand(ApplyFilterAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        NextPageCommand = new AsyncRelayCommand(NextPageAsync, CanNext);
        PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync, CanPrevious);
        StartCreateCommand = new RelayCommand(StartCreate);
        StartEditCommand = new RelayCommand(StartEdit);
        BeginDeleteCommand = new RelayCommand(() => Mode = MenuDesktopMode.DeleteConfirmation);
        CancelDeleteCommand = new RelayCommand(() => Mode = MenuDesktopMode.Detail);
        CancelFormCommand = new RelayCommand(CancelForm);
        SaveCommand = new AsyncRelayCommand(SaveAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        AddDrinkCommand = new AsyncRelayCommand(AddDrinkAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        RemoveDrinkCommand = new AsyncRelayCommand<MenuItemViewModel>(RemoveDrinkAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        PublishCommand = new AsyncRelayCommand(PublishAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        DraftCommand = new AsyncRelayCommand(DraftAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public static Func<IDesktopWorkspace> CreateFactory(
        MenusModule menus,
        DrinksModule drinks,
        MenuActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor,
        IUiDispatcher? dispatcher = null) => () => new MenusViewModel(
            new ModuleMenuDesktopOperations(menus, drinks, projector, taggedMutations, session, actor), dispatcher);

    public WorkspaceId Id => NavigationProjector.MenusWorkspace;
    public string Title => "Menus";
    public bool IsDirty => isDirty;
    public bool IsBrowse => Mode == MenuDesktopMode.Browse;
    public bool IsDetail => Mode is MenuDesktopMode.Detail or MenuDesktopMode.DeleteConfirmation;
    public bool IsForm => Mode is MenuDesktopMode.Create or MenuDesktopMode.Edit;
    public bool IsDeleteConfirmation => Mode == MenuDesktopMode.DeleteConfirmation;
    public bool CanCreate => Enabled(MenuActionProjector.CreateAction);
    public bool CanEdit => Enabled(MenuActionProjector.EditAction);
    public bool CanDelete => Enabled(MenuActionProjector.DeleteAction);
    public bool CanTags => Enabled(MenuActionProjector.TagsAction);
    public bool CanEditTags => Mode == MenuDesktopMode.Create || CanTags;
    public bool CanAddDrink => Enabled(MenuActionProjector.AddDrinkAction);
    public bool CanRemoveDrink => Enabled(MenuActionProjector.RemoveDrinkAction);
    public bool CanPublish => Enabled(MenuActionProjector.PublishAction);
    public bool CanDraft => Enabled(MenuActionProjector.DraftAction);
    public bool CanAnalyze => Enabled(MenuActionProjector.ReadinessAction);
    public IReadOnlyList<string> Statuses { get; } = ["all", .. MenuStatus.All.Select(static value => value.Value)];
    public string FilterHelp => "Fields: id, name, description, status, created_at, published_at, tags.";
    public ObservableCollection<MenuRowViewModel> Rows { get; } = [];
    public ObservableCollection<MenuItemViewModel> Items { get; } = [];
    public ObservableCollection<MenuDrinkOption> DrinkOptions { get; } = [];
    public ObservableCollection<ReadinessFinding> Findings { get; } = [];
    public ObservableCollection<MenuItemAnalysis> AnalysisItems { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand ApplyFilterCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public IAsyncRelayCommand PreviousPageCommand { get; }
    public IRelayCommand StartCreateCommand { get; }
    public IRelayCommand StartEditCommand { get; }
    public IRelayCommand BeginDeleteCommand { get; }
    public IRelayCommand CancelDeleteCommand { get; }
    public IRelayCommand CancelFormCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IAsyncRelayCommand AddDrinkCommand { get; }
    public IAsyncRelayCommand<MenuItemViewModel> RemoveDrinkCommand { get; }
    public IAsyncRelayCommand PublishCommand { get; }
    public IAsyncRelayCommand DraftCommand { get; }
    public IAsyncRelayCommand AnalyzeCommand { get; }
    public Exception? Error { get; private set; }

    public MenuDesktopMode Mode { get => mode; private set { if (SetProperty(ref mode, value)) { NotifyMode(); } } }
    public MenuRowViewModel? Selected { get => selected; set { if (SetProperty(ref selected, value)) { active = SelectAsync(value); } } }
    public Menu? Detail { get => detail; private set => SetProperty(ref detail, value); }

    [ObservableProperty] private string filterStatus = "all";
    [ObservableProperty] private string filterExpression = string.Empty;
    [ObservableProperty] private string pageSize = "100";
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private string tags = string.Empty;
    [ObservableProperty] private MenuDrinkOption? selectedDrink;
    [ObservableProperty] private string targetMargin = "0.70";
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isSubmitting;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string analysisSummary = string.Empty;

    partial void OnNameChanged(string value) => UpdateDirty();
    partial void OnDescriptionChanged(string value) => UpdateDirty();
    partial void OnTagsChanged(string value) => UpdateDirty();

    public Task ActivateAsync(CancellationToken cancellationToken = default) => LoadAsync(cursor, cancellationToken);

    public async Task DrainAsync()
    {
        while (true)
        {
            Task snapshot = active;
            await snapshot.ConfigureAwait(false);
            if (ReferenceEquals(snapshot, active))
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
        await loads.DisposeAsync().ConfigureAwait(false);
        await mutations.DisposeAsync().ConfigureAwait(false);
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

            MenuStatus? status = FilterStatus == "all" ? null : MenuStatus.Parse(FilterStatus);
            request = new ListMenusRequest(status, FilterExpression, Limit: limit).Normalize();
            cursor = next = default;
            history.Clear();
            await LoadAsync(default, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (!AppError.IsCancellation(exception)) { PublishError(Safe(exception, "apply menu filter")); }
    }

    private async Task NextPageAsync()
    {
        if (!CanNext())
        {
            return;
        }

        history.Add(cursor); cursor = next;
        await LoadAsync(cursor, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task PreviousPageAsync()
    {
        if (!CanPrevious())
        {
            return;
        }

        int index = history.Count - 1; cursor = history[index]; history.RemoveAt(index);
        await LoadAsync(cursor, CancellationToken.None).ConfigureAwait(false);
    }

    private bool CanNext() => !IsLoading && !next.IsEmpty;
    private bool CanPrevious() => !IsLoading && history.Count > 0;

    private async Task LoadAsync(Cursor page, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await dispatcher.InvokeAsync(() => { IsLoading = true; StatusMessage = "Loading menus…"; NotifyPaging(); }, cancellationToken).ConfigureAwait(false);
        try
        {
            ListMenusRequest snapshot = request with { Cursor = page };
            LatestResult<MenuLoadOutcome> latest = await loads.RunAsync(async token =>
            {
                try
                {
                    Task<Page<Menu>> pageTask = operations.ListAsync(snapshot, token);
                    Task<IReadOnlyList<Drink>> drinksTask = operations.DrinksAsync(token);
                    Task<IReadOnlyList<ActionState>> actionsTask = operations.ProjectAsync(null, token);
                    await Task.WhenAll(pageTask, drinksTask, actionsTask).ConfigureAwait(false);
                    return new MenuLoadOutcome(await pageTask, await drinksTask, await actionsTask, null);
                }
                catch (Exception exception) when (AppError.IsCancellation(exception)) { throw; }
                catch (Exception exception) { return new(null, [], [], Safe(exception, "load desktop menus")); }
            }, cancellationToken).ConfigureAwait(false);
            if (latest.IsCurrent && latest.Value is not null)
            {
                await dispatcher.InvokeAsync(() => PublishLoad(latest.Value), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (AppError.IsCancellation(exception) && !cancellationToken.IsCancellationRequested) { }
    }

    private void PublishLoad(MenuLoadOutcome outcome)
    {
        if (outcome.Error is not null || outcome.Page is null) { PublishError(outcome.Error!); return; }
        string? selectedId = Selected?.Id;
        drinkNames.Clear(); DrinkOptions.Clear();
        foreach (Drink drink in outcome.Drinks.OrderBy(static value => value.Name, StringComparer.CurrentCultureIgnoreCase))
        { drinkNames[drink.Id] = drink.Name; DrinkOptions.Add(new(drink.Id, drink.Name)); }
        actions = outcome.Actions;
        Rows.Clear(); foreach (Menu menu in outcome.Page.Items)
        {
            Rows.Add(new(menu));
        }

        next = outcome.Page.Next;
        MenuRowViewModel? keep = Rows.FirstOrDefault(row => row.Id == selectedId);
        if (!ReferenceEquals(Selected, keep))
        {
            Selected = keep;
        }

        Error = null; IsLoading = false; StatusMessage = $"{Rows.Count} menus"; NotifyActions(); NotifyPaging();
    }

    private async Task SelectAsync(MenuRowViewModel? row)
    {
        if (row is null) { Detail = null; Items.Clear(); Findings.Clear(); AnalysisItems.Clear(); Mode = MenuDesktopMode.Browse; return; }
        await RunLoadAsync(async token =>
        {
            Menu menu = await operations.GetAsync(row.Menu.Id, token).ConfigureAwait(false);
            IReadOnlyList<ActionState> projected = await operations.ProjectAsync(menu, token).ConfigureAwait(false);
            ReadinessReport? readiness = Enabled(projected, MenuActionProjector.ReadinessAction)
                ? await operations.ReadinessAsync(menu.Id, token).ConfigureAwait(false) : null;
            if (readiness is not null)
            {
                projected = MenuActionProjector.ApplyReadiness(projected, readiness);
            }

            return MenuMutationOutcome.Success(menu, projected, readiness, null);
        }, "load menu detail").ConfigureAwait(false);
    }

    private void StartCreate()
    {
        Detail = null; Name = Description = Tags = string.Empty; baseline = Snapshot(); SetDirty(false); Mode = MenuDesktopMode.Create;
    }

    private void StartEdit()
    {
        if (Detail is null || !CanEdit)
        {
            return;
        }

        Name = Detail.Name; Description = Detail.Description; Tags = Detail.Tags.Format(); baseline = Snapshot(); SetDirty(false); Mode = MenuDesktopMode.Edit;
    }

    private void CancelForm()
    {
        SetDirty(false); Mode = Detail is null ? MenuDesktopMode.Browse : MenuDesktopMode.Detail;
    }

    private async Task SaveAsync()
    {
        try
        {
            TagCollection? parsedTags = Mode == MenuDesktopMode.Edit && !CanTags
                ? null
                : TagCollection.Parse(Tags);
            await RunMutationAsync(async token => Mode switch
            {
                MenuDesktopMode.Create => await operations.CreateAsync(new(Name, Description), parsedTags, token).ConfigureAwait(false),
                MenuDesktopMode.Edit when Detail is not null => await operations.UpdateAsync(new(Detail.Id, Name, Description), parsedTags, token).ConfigureAwait(false),
                _ => throw AppError.Invalid("menu form is not active"),
            }, "save menu").ConfigureAwait(false);
        }
        catch (Exception exception) when (!AppError.IsCancellation(exception)) { PublishError(Safe(exception, "save menu")); }
    }

    private Task DeleteAsync() => Detail is null ? Task.CompletedTask : RunMutationAsync(token => operations.DeleteAsync(Detail.Id, token), "delete menu", true);
    private Task AddDrinkAsync() => Detail is null || SelectedDrink is null ? Task.CompletedTask :
        RunMutationAsync(token => operations.AddDrinkAsync(new(Detail.Id, SelectedDrink.Id), token), "add menu drink");
    private Task RemoveDrinkAsync(MenuItemViewModel? item) => Detail is null || item is null ? Task.CompletedTask :
        RunMutationAsync(token => operations.RemoveDrinkAsync(new(Detail.Id, item.Item.DrinkId), token), "remove menu drink");
    private Task PublishAsync() => Detail is null ? Task.CompletedTask : RunMutationAsync(token => operations.PublishAsync(Detail.Id, token), "publish menu");
    private Task DraftAsync() => Detail is null ? Task.CompletedTask : RunMutationAsync(token => operations.DraftAsync(Detail.Id, token), "return menu to draft");

    private async Task AnalyzeAsync()
    {
        if (Detail is null)
        {
            return;
        }

        try
        {
            if (!double.TryParse(TargetMargin, NumberStyles.Float, CultureInfo.InvariantCulture, out double margin))
            {
                throw AppError.Invalid("target margin must be a number between 0 and 1");
            }

            await RunLoadAsync(async token =>
            {
                MenuAnalysis analysis = await operations.AnalyzeAsync(Detail.Id, margin, token).ConfigureAwait(false);
                IReadOnlyList<ActionState> projected = await operations.ProjectAsync(analysis.Menu, token).ConfigureAwait(false);
                ReadinessReport readiness = await operations.ReadinessAsync(analysis.Menu.Id, token).ConfigureAwait(false);
                return MenuMutationOutcome.Success(analysis.Menu, MenuActionProjector.ApplyReadiness(projected, readiness), readiness, analysis);
            }, "analyze menu").ConfigureAwait(false);
        }
        catch (Exception exception) when (!AppError.IsCancellation(exception)) { PublishError(Safe(exception, "analyze menu")); }
    }

    private async Task RunMutationAsync(Func<CancellationToken, Task<Menu>> work, string operation, bool deleted = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this); IsSubmitting = true; StatusMessage = "Saving menu…";
        try
        {
            LatestResult<MenuMutationOutcome> latest = await mutations.RunAsync(async token =>
            {
                try
                {
                    Menu menu = await work(token).ConfigureAwait(false);
                    IReadOnlyList<ActionState> projected = await operations.ProjectAsync(deleted ? null : menu, token).ConfigureAwait(false);
                    ReadinessReport? readiness = !deleted && Enabled(projected, MenuActionProjector.ReadinessAction)
                        ? await operations.ReadinessAsync(menu.Id, token).ConfigureAwait(false) : null;
                    if (readiness is not null)
                    {
                        projected = MenuActionProjector.ApplyReadiness(projected, readiness);
                    }

                    return MenuMutationOutcome.Success(menu, projected, readiness, null, deleted);
                }
                catch (Exception exception) when (AppError.IsCancellation(exception)) { throw; }
                catch (Exception exception) { return MenuMutationOutcome.Failed(Safe(exception, operation)); }
            }).ConfigureAwait(false);
            if (latest.IsCurrent && latest.Value is not null)
            {
                await dispatcher.InvokeAsync(() => PublishMutation(latest.Value)).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (AppError.IsCancellation(exception)) { throw; }
    }

    private async Task RunLoadAsync(Func<CancellationToken, Task<MenuMutationOutcome>> work, string operation)
    {
        IsLoading = true; StatusMessage = "Loading menu…";
        try
        {
            LatestResult<MenuLoadOutcome> latest = await loads.RunAsync(async token =>
            {
                try { return new(null, [], [], null, await work(token).ConfigureAwait(false)); }
                catch (Exception exception) when (AppError.IsCancellation(exception)) { throw; }
                catch (Exception exception) { return new(null, [], [], Safe(exception, operation)); }
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

    private void PublishMutation(MenuMutationOutcome outcome)
    {
        if (outcome.Error is not null) { PublishError(outcome.Error); return; }
        IsSubmitting = IsLoading = false; Error = null; SetDirty(false);
        if (outcome.Deleted) { Detail = null; Selected = null; Mode = MenuDesktopMode.Browse; StatusMessage = "Menu deleted"; _ = LoadAsync(cursor, CancellationToken.None); return; }
        Menu menu = outcome.Menu!; Detail = menu; actions = outcome.Actions;
        MenuRowViewModel? existing = Rows.FirstOrDefault(row => row.Id == menu.Id.Value);
        if (existing is not null)
        {
            Rows[Rows.IndexOf(existing)] = new(menu);
        }
        else
        {
            Rows.Insert(0, new(menu));
        }

        selected = Rows.First(row => row.Id == menu.Id.Value); OnPropertyChanged(nameof(Selected));
        Items.Clear(); foreach (MenuItem item in menu.Items)
        {
            Items.Add(new(item, drinkNames.GetValueOrDefault(item.DrinkId, item.DisplayName ?? item.DrinkId.Value)));
        }

        Findings.Clear(); foreach (ReadinessFinding finding in outcome.Readiness?.Findings ?? [])
        {
            Findings.Add(finding);
        }

        AnalysisItems.Clear(); foreach (MenuItemAnalysis item in outcome.Analysis?.Items ?? [])
        {
            AnalysisItems.Add(item);
        }

        AnalysisSummary = outcome.Analysis is null ? string.Empty : $"{outcome.Analysis.AvailableCount}/{outcome.Analysis.TotalCount} available · average margin {outcome.Analysis.AverageMargin:P1}";
        Name = menu.Name; Description = menu.Description; Tags = menu.Tags.Format(); baseline = Snapshot(); SetDirty(false);
        Mode = MenuDesktopMode.Detail; StatusMessage = "Menu ready"; NotifyActions();
    }

    private void UpdateDirty() { if (IsForm)
        {
            SetDirty(Snapshot() != baseline);
        }
    }
    private string Snapshot() => string.Join('\u001f', Name, Description, Tags);
    private void SetDirty(bool value) { if (isDirty == value) { return; } isDirty = value; OnPropertyChanged(nameof(IsDirty)); }
    private bool Enabled(ActionId id) => Enabled(actions, id);
    private static bool Enabled(IEnumerable<ActionState> states, ActionId id) => states.Any(state => state.Id == id && state.Visible && state.Enabled);
    private static Exception Safe(Exception exception, string operation) => AppError.Find(exception) is not null || AppError.IsCancellation(exception) ? exception : AppError.Internal(operation, exception);
    private void PublishError(Exception exception) { Error = exception; IsLoading = IsSubmitting = false; StatusMessage = AppError.Find(exception)?.UserMessage ?? "internal error"; NotifyPaging(); }
    private void NotifyPaging() { NextPageCommand.NotifyCanExecuteChanged(); PreviousPageCommand.NotifyCanExecuteChanged(); }
    private void NotifyMode() { OnPropertyChanged(nameof(IsBrowse)); OnPropertyChanged(nameof(IsDetail)); OnPropertyChanged(nameof(IsForm)); OnPropertyChanged(nameof(IsDeleteConfirmation)); OnPropertyChanged(nameof(CanEditTags)); }
    private void NotifyActions() { OnPropertyChanged(nameof(CanCreate)); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanDelete)); OnPropertyChanged(nameof(CanTags)); OnPropertyChanged(nameof(CanEditTags)); OnPropertyChanged(nameof(CanAddDrink)); OnPropertyChanged(nameof(CanRemoveDrink)); OnPropertyChanged(nameof(CanPublish)); OnPropertyChanged(nameof(CanDraft)); OnPropertyChanged(nameof(CanAnalyze)); }

    private sealed record MenuLoadOutcome(Page<Menu>? Page, IReadOnlyList<Drink> Drinks, IReadOnlyList<ActionState> Actions, Exception? Error, MenuMutationOutcome? Detail = null);
    private sealed record MenuMutationOutcome(Menu? Menu, IReadOnlyList<ActionState> Actions, ReadinessReport? Readiness, MenuAnalysis? Analysis, bool Deleted, Exception? Error)
    {
        public static MenuMutationOutcome Success(Menu menu, IReadOnlyList<ActionState> actions, ReadinessReport? readiness, MenuAnalysis? analysis, bool deleted = false) => new(menu, actions, readiness, analysis, deleted, null);
        public static MenuMutationOutcome Failed(Exception error) => new(null, [], null, null, false, error);
    }
}
