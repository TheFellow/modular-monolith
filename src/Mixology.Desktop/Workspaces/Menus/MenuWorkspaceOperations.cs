using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Presentation;
using Mixology.Modules.Menus.Requests;
using Mixology.Presentation.Mutations;

namespace Mixology.Desktop.Workspaces.Menus;

public interface IMenuDesktopOperations
{
    Task<Page<Menu>> ListAsync(ListMenusRequest request, CancellationToken cancellationToken);
    Task<Menu> GetAsync(MenuId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectAsync(Menu? selected, CancellationToken cancellationToken);
    Task<IReadOnlyList<Drink>> DrinksAsync(CancellationToken cancellationToken);
    Task<Menu> CreateAsync(CreateMenuRequest request, TagCollection? tags, CancellationToken cancellationToken);
    Task<Menu> UpdateAsync(UpdateMenuRequest request, TagCollection? tags, CancellationToken cancellationToken);
    Task<Menu> DeleteAsync(MenuId id, CancellationToken cancellationToken);
    Task<Menu> AddDrinkAsync(AddMenuItemRequest request, CancellationToken cancellationToken);
    Task<Menu> RemoveDrinkAsync(RemoveMenuItemRequest request, CancellationToken cancellationToken);
    Task<Menu> PublishAsync(MenuId id, CancellationToken cancellationToken);
    Task<Menu> DraftAsync(MenuId id, CancellationToken cancellationToken);
    Task<ReadinessReport> ReadinessAsync(MenuId id, CancellationToken cancellationToken);
    Task<MenuAnalysis> AnalyzeAsync(MenuId id, double targetMargin, CancellationToken cancellationToken);
}

internal sealed class ModuleMenuDesktopOperations(
    MenusModule menus,
    DrinksModule drinks,
    MenuActionProjector projector,
    TaggedMutationCoordinator taggedMutations,
    MixologySession session,
    Actor actor) : IMenuDesktopOperations
{
    public Task<Page<Menu>> ListAsync(ListMenusRequest request, CancellationToken cancellationToken) =>
        menus.ListAsync(session, request, cancellationToken);

    public Task<Menu> GetAsync(MenuId id, CancellationToken cancellationToken) =>
        menus.GetAsync(session, id, cancellationToken);

    public Task<IReadOnlyList<ActionState>> ProjectAsync(Menu? selected, CancellationToken cancellationToken) =>
        projector.ProjectAsync(actor, selected, cancellationToken);

    public async Task<IReadOnlyList<Drink>> DrinksAsync(CancellationToken cancellationToken)
    {
        List<Drink> result = [];
        Cursor cursor = default;
        do
        {
            Page<Drink> page = await drinks.ListAsync(
                session,
                new ListDrinksRequest(Cursor: cursor),
                cancellationToken).ConfigureAwait(false);
            result.AddRange(page.Items);
            cursor = page.Next;
        }
        while (!cursor.IsEmpty);

        return result;
    }

    public Task<Menu> CreateAsync(
        CreateMenuRequest request,
        TagCollection? tags,
        CancellationToken cancellationToken) => taggedMutations.RunAsync(
            session,
            (active, token) => menus.CreateAsync(active, request, token),
            tags,
            static value => value.EntityUid,
            static (value, applied) => value with { Tags = applied },
            cancellationToken);

    public Task<Menu> UpdateAsync(
        UpdateMenuRequest request,
        TagCollection? tags,
        CancellationToken cancellationToken) => taggedMutations.RunAsync(
            session,
            (active, token) => menus.UpdateAsync(active, request, token),
            tags,
            static value => value.EntityUid,
            static (value, applied) => value with { Tags = applied },
            cancellationToken);

    public Task<Menu> DeleteAsync(MenuId id, CancellationToken cancellationToken) =>
        menus.DeleteAsync(session, id, cancellationToken);

    public Task<Menu> AddDrinkAsync(AddMenuItemRequest request, CancellationToken cancellationToken) =>
        menus.AddDrinkAsync(session, request, cancellationToken);

    public Task<Menu> RemoveDrinkAsync(RemoveMenuItemRequest request, CancellationToken cancellationToken) =>
        menus.RemoveDrinkAsync(session, request, cancellationToken);

    public Task<Menu> PublishAsync(MenuId id, CancellationToken cancellationToken) =>
        menus.PublishAsync(session, id, cancellationToken);

    public Task<Menu> DraftAsync(MenuId id, CancellationToken cancellationToken) =>
        menus.DraftAsync(session, id, cancellationToken);

    public Task<ReadinessReport> ReadinessAsync(MenuId id, CancellationToken cancellationToken) =>
        menus.ReadinessAsync(session, id, cancellationToken);

    public Task<MenuAnalysis> AnalyzeAsync(
        MenuId id,
        double targetMargin,
        CancellationToken cancellationToken) => menus.AnalyzeAsync(session, id, targetMargin, cancellationToken);
}
