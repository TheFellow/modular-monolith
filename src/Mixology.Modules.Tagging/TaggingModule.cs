using Cedar.Types;
using Mixology.Application;
using Mixology.Application.Operations;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Tags;
using Mixology.Modules.Tagging.Authorization;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence;
using EntityIds = Mixology.Kernel.Entities.EntityIds;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Tagging;

public sealed class TaggingModule(
    MixologyStore store,
    TagRepository repository,
    TagTargetRegistry registry,
    IEntityAuthorizer authorizer)
{
    public Task<TagMutationResult> UpsertAsync(
        MixologySession session,
        KernelEntityUid target,
        Tag value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        TagTargetRegistration registration = Resolve(target);
        value.Validate();
        return session.ExecuteAsync(
            Command(registration.TagAction),
            async context =>
            {
                TargetState current = await LoadStateAsync(
                    context.Session!, registration, target, context.CancellationToken).ConfigureAwait(false);
                await AuthorizeAsync(context, registration.TagAction, current.Entity).ConfigureAwait(false);
                bool changed = await repository.UpsertAsync(
                    context.Session!, target, value, context.CancellationToken).ConfigureAwait(false);
                TagCollection tags = changed ? current.Tags.Upsert(value) : current.Tags;
                await AuthorizeAsync(
                    context,
                    registration.TagAction,
                    WithTags(current.Entity, tags)).ConfigureAwait(false);
                Record(context, target, changed);
                return new TagMutationResult(target, tags, changed);
            },
            cancellationToken);
    }

    public Task<TagMutationResult> SetAsync(
        MixologySession session,
        KernelEntityUid target,
        Tag value,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(session, target, value, cancellationToken);

    public Task<TagMutationResult> ReplaceAsync(
        MixologySession session,
        KernelEntityUid target,
        TagCollection desired,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(desired);
        TagTargetRegistration registration = Resolve(target);
        desired.Validate();
        return session.ExecuteAsync(
            Command(registration.TagAction),
            async context =>
            {
                TargetState current = await LoadStateAsync(
                    context.Session!, registration, target, context.CancellationToken).ConfigureAwait(false);
                List<KernelEntityUid> actions = ReplaceActions(registration, current.Tags, desired);
                foreach (KernelEntityUid action in actions)
                {
                    await AuthorizeAsync(context, action, current.Entity).ConfigureAwait(false);
                }

                bool changed = await repository.ReplaceAsync(
                    context.Session!, target, desired, context.CancellationToken).ConfigureAwait(false);
                Entity result = WithTags(current.Entity, desired);
                foreach (KernelEntityUid action in actions)
                {
                    await AuthorizeAsync(context, action, result).ConfigureAwait(false);
                }

                Record(context, target, changed);
                return new TagMutationResult(target, desired, changed);
            },
            cancellationToken);
    }

    public Task<TagMutationResult> RemoveAsync(
        MixologySession session,
        KernelEntityUid target,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        TagTargetRegistration registration = Resolve(target);
        new Tag(key).Validate();
        return session.ExecuteAsync(
            Command(registration.UntagAction),
            async context =>
            {
                TargetState current = await LoadStateAsync(
                    context.Session!, registration, target, context.CancellationToken).ConfigureAwait(false);
                await AuthorizeAsync(context, registration.UntagAction, current.Entity).ConfigureAwait(false);
                bool changed = await repository.RemoveAsync(
                    context.Session!, target, key, context.CancellationToken).ConfigureAwait(false);
                TagCollection tags = changed ? current.Tags.Remove(key) : current.Tags;
                await AuthorizeAsync(
                    context,
                    registration.UntagAction,
                    WithTags(current.Entity, tags)).ConfigureAwait(false);
                Record(context, target, changed);
                return new TagMutationResult(target, tags, changed);
            },
            cancellationToken);
    }

    public Task<TagCollection> ListAsync(
        MixologySession session,
        KernelEntityUid target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        TagTargetRegistration registration = Resolve(target);
        return session.ExecuteAsync(
            Query(registration.GetAction),
            async context =>
            {
                await using StoreSession read = await store.OpenSessionAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                TargetState state = await LoadStateAsync(
                    read, registration, target, context.CancellationToken).ConfigureAwait(false);
                await AuthorizeAsync(context, registration.GetAction, state.Entity).ConfigureAwait(false);
                return state.Tags;
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<TagReference>> ShowAsync(
        MixologySession session,
        Tag value,
        bool exact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        value.Validate();
        return session.ExecuteAsync(
            Query(TaggingAuthorization.Show),
            async context =>
            {
                Entity resource = TaggingAuthorization.DiscoveryResource("show", value.Key, value.Value, exact);
                await AuthorizeAsync(context, TaggingAuthorization.Show, resource).ConfigureAwait(false);
                await using StoreSession read = await store.OpenSessionAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                TagAssociation[] associations = await repository.FindAsync(
                    read, value, exact, context.CancellationToken).ConfigureAwait(false);
                TagAssociation[] active = await ActiveAssociationsAsync(
                    read, associations, context.CancellationToken).ConfigureAwait(false);
                Dictionary<KernelEntityUid, string> names = [];
                List<TagReference> result = new(active.Length);
                foreach (TagAssociation association in active)
                {
                    if (!names.TryGetValue(association.Target, out string? name))
                    {
                        TagTargetRegistration registration = registry.Resolve(association.Target.Type);
                        TargetState state = await LoadStateAsync(
                            read, registration, association.Target, context.CancellationToken).ConfigureAwait(false);
                        name = state.DisplayName;
                        names[association.Target] = name;
                    }

                    result.Add(new TagReference(
                        EntityTypeName(association.Target.Type),
                        name,
                        association.Target.Id,
                        association.Tag.ToString()));
                }

                return (IReadOnlyList<TagReference>)result;
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<TagSummary>> SummaryAsync(
        MixologySession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.ExecuteAsync(
            Query(TaggingAuthorization.Summary),
            async context =>
            {
                await AuthorizeAsync(
                    context,
                    TaggingAuthorization.Summary,
                    TaggingAuthorization.DiscoveryResource("summary")).ConfigureAwait(false);
                await using StoreSession read = await store.OpenSessionAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                TagAssociation[] associations = await repository.AllAsync(read, context.CancellationToken)
                    .ConfigureAwait(false);
                TagAssociation[] active = await ActiveAssociationsAsync(
                    read, associations, context.CancellationToken).ConfigureAwait(false);
                TagSummary[] result = active.GroupBy(association => association.Tag.ToString(), StringComparer.Ordinal)
                    .Select(group => new TagSummary(
                        group.Key,
                        group.Count(),
                        group.Count(value => value.Target.Type == EntityIds.DrinkType),
                        group.Count(value => value.Target.Type == EntityIds.IngredientType),
                        group.Count(value => value.Target.Type == EntityIds.InventoryType),
                        group.Count(value => value.Target.Type == EntityIds.MenuType),
                        group.Count(value => value.Target.Type == EntityIds.OrderType)))
                    .OrderByDescending(summary => summary.Total)
                    .ThenBy(summary => summary.Tag, StringComparer.Ordinal)
                    .ToArray();
                return (IReadOnlyList<TagSummary>)result;
            },
            cancellationToken);
    }

    private TagTargetRegistration Resolve(KernelEntityUid target)
    {
        TagRepository.ValidateTarget(target);
        return registry.Resolve(target.Type);
    }

    private async ValueTask<TargetState> LoadStateAsync(
        StoreSession session,
        TagTargetRegistration registration,
        KernelEntityUid target,
        CancellationToken cancellationToken)
    {
        TagTargetState loaded = await registration.LoadAsync(session, target.Id, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Entity.Uid != target.ToCedarUid())
        {
            throw AppError.Internal($"tag target loader returned {loaded.Entity.Uid} for {target.Type}::{target.Id}");
        }

        string name = loaded.DisplayName?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            throw AppError.Internal($"tag target loader returned an empty display name for {target.Type}::{target.Id}");
        }

        TagCollection tags = await repository.ListAsync(session, target, cancellationToken).ConfigureAwait(false);
        Entity entity = WithTags(loaded.Entity, tags);
        return new TargetState(entity, name, tags);
    }

    private static Entity WithTags(Entity entity, TagCollection tags)
    {
        Dictionary<CedarString, ICedarData> cedarTags = tags.ToDictionary().ToDictionary(
            static pair => new CedarString(pair.Key),
            static pair => (ICedarData)new CedarString(pair.Value));
        return new Entity(
            entity.Uid,
            entity.Parents,
            entity.Attributes,
            new CedarRecord(cedarTags));
    }

    private async Task<TagAssociation[]> ActiveAssociationsAsync(
        StoreSession session,
        IReadOnlyCollection<TagAssociation> associations,
        CancellationToken cancellationToken)
    {
        Dictionary<string, IReadOnlySet<string>> activeByType = new(StringComparer.Ordinal);
        foreach (IGrouping<string, TagAssociation> group in associations.GroupBy(
                     association => association.Target.Type,
                     StringComparer.Ordinal))
        {
            TagTargetRegistration registration = registry.Resolve(group.Key);
            string[] ids = group.Select(value => value.Target.Id).Distinct(StringComparer.Ordinal).ToArray();
            activeByType[group.Key] = await registration.ActiveIdsAsync(session, ids, cancellationToken)
                .ConfigureAwait(false);
        }

        return associations.Where(association =>
                activeByType[association.Target.Type].Contains(association.Target.Id))
            .ToArray();
    }

    private ValueTask AuthorizeAsync(OperationContext context, KernelEntityUid action, Entity resource) =>
        authorizer.AuthorizeAsync(context.Principal, action, resource, context.CancellationToken);

    private static List<KernelEntityUid> ReplaceActions(
        TagTargetRegistration registration,
        TagCollection current,
        TagCollection desired)
    {
        IReadOnlyDictionary<string, string> currentValues = current.ToDictionary();
        IReadOnlyDictionary<string, string> desiredValues = desired.ToDictionary();
        bool requiresTag = desiredValues.Any(pair =>
            !currentValues.TryGetValue(pair.Key, out string? value) ||
            !string.Equals(value, pair.Value, StringComparison.Ordinal));
        bool requiresUntag = currentValues.Keys.Any(key => !desiredValues.ContainsKey(key));
        List<KernelEntityUid> actions = [];
        if (requiresTag || !requiresUntag)
        {
            actions.Add(registration.TagAction);
        }

        if (requiresUntag)
        {
            actions.Add(registration.UntagAction);
        }

        return actions;
    }

    private static void Record(OperationContext context, KernelEntityUid target, bool changed)
    {
        context.SelectResource(target);
        if (changed)
        {
            context.Touch(target);
        }
    }

    private static string EntityTypeName(string type) => type switch
    {
        EntityIds.DrinkType => "Drink",
        EntityIds.IngredientType => "Ingredient",
        EntityIds.InventoryType => "Inventory",
        EntityIds.MenuType => "Menu",
        EntityIds.OrderType => "Order",
        _ => type,
    };

    private static Operation Command(KernelEntityUid action) => Operation.Command(ActionName(action));
    private static Operation Query(KernelEntityUid action) => Operation.Query(ActionName(action));
    private static string ActionName(KernelEntityUid action) => $"{action.Type}::\"{action.Id}\"";

    private sealed record TargetState(Entity Entity, string DisplayName, TagCollection Tags);
}
