using Cedar.Types;
using Mixology.Kernel.Tags;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Tagging.Models;

public sealed record TagMutationResult(KernelEntityUid Target, TagCollection Tags, bool Changed);

public sealed record TagReference(
    string EntityType,
    string EntityName,
    string EntityId,
    string Tag);

public sealed record TagSummary(
    string Tag,
    int Total,
    int Drinks,
    int Ingredients,
    int Inventory,
    int Menus,
    int Orders);

public sealed record TagTargetState(Entity Entity, string DisplayName);
