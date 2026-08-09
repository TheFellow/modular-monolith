using Mixology.Kernel.Errors;

namespace Mixology.Application.Presentation.Actions;

public readonly record struct ActionId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct ActionConditionResult(bool Available, string DisabledReason = "")
{
    public static ActionConditionResult Enabled { get; } = new(true);

    public static ActionConditionResult Disabled(string reason) => new(false, reason ?? string.Empty);
}

public delegate ValueTask ActionAuthorization(CancellationToken cancellationToken);

public delegate ValueTask<ActionConditionResult> ActionCondition(CancellationToken cancellationToken);

internal enum ActionPermissionMode : byte
{
    Inherit,
    Public,
    Required,
}

public sealed class ActionPermission
{
    private ActionPermission(ActionPermissionMode mode, ActionAuthorization? authorize)
    {
        Mode = mode;
        Authorize = authorize;
    }

    public static ActionPermission Inherit { get; } = new(ActionPermissionMode.Inherit, null);

    public static ActionPermission Public { get; } = new(ActionPermissionMode.Public, null);

    public static ActionPermission Require(ActionAuthorization authorize)
    {
        ArgumentNullException.ThrowIfNull(authorize);
        return new ActionPermission(ActionPermissionMode.Required, authorize);
    }

    internal ActionPermissionMode Mode { get; }

    internal ActionAuthorization? Authorize { get; }
}

public sealed record ActionControl(
    ActionId Id,
    ActionPermission? Permission = null,
    IReadOnlyList<ActionCondition>? Conditions = null);

public sealed record ActionGroup(
    ActionPermission? Permission = null,
    IReadOnlyList<ActionControl>? Controls = null,
    IReadOnlyList<ActionGroup>? Groups = null);

public sealed record ActionState(
    ActionId Id,
    bool Visible,
    bool Enabled,
    string DisabledReason = "");

public static class ActionProjector
{
    public static async Task<IReadOnlyList<ActionState>> EvaluateAsync(
        ActionGroup declaration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        List<ActionState> states = [];
        HashSet<ActionId> seen = [];
        await EvaluateGroupAsync(
            declaration,
            inherited: null,
            seen,
            states,
            cancellationToken).ConfigureAwait(false);
        return states;
    }

    private static async Task EvaluateGroupAsync(
        ActionGroup group,
        ActionAuthorization? inherited,
        HashSet<ActionId> seen,
        List<ActionState> states,
        CancellationToken cancellationToken)
    {
        ActionAuthorization? authorize = ResolvePermission(group.Permission, inherited);
        foreach (ActionControl control in group.Controls ?? [])
        {
            if (control is null)
            {
                throw AppError.Internal("action declaration contains a missing control");
            }

            if (control.Id.IsEmpty)
            {
                throw AppError.Internal("action control id must not be empty");
            }

            if (!seen.Add(control.Id))
            {
                throw AppError.Internal($"duplicate action control id {control.Id}");
            }

            ActionAuthorization? controlAuthorize = ResolvePermission(control.Permission, authorize);
            states.Add(await EvaluateControlAsync(
                control,
                controlAuthorize,
                cancellationToken).ConfigureAwait(false));
        }

        foreach (ActionGroup child in group.Groups ?? [])
        {
            if (child is null)
            {
                throw AppError.Internal("action declaration contains a missing group");
            }

            await EvaluateGroupAsync(
                child,
                authorize,
                seen,
                states,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static ActionAuthorization? ResolvePermission(
        ActionPermission? permission,
        ActionAuthorization? inherited)
    {
        ActionPermission resolved = permission ?? ActionPermission.Inherit;
        return resolved.Mode switch
        {
            ActionPermissionMode.Inherit => inherited,
            ActionPermissionMode.Public => null,
            ActionPermissionMode.Required when resolved.Authorize is not null => resolved.Authorize,
            _ => throw AppError.Internal("invalid action permission declaration"),
        };
    }

    private static async Task<ActionState> EvaluateControlAsync(
        ActionControl control,
        ActionAuthorization? authorize,
        CancellationToken cancellationToken)
    {
        if (authorize is not null)
        {
            try
            {
                await authorize(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (AppError.IsPermission(exception))
            {
                return new ActionState(control.Id, Visible: false, Enabled: false);
            }
            catch (Exception exception) when (
                AppError.Find(exception) is null && !AppError.IsCancellation(exception))
            {
                throw AppError.Internal($"authorize projected action {control.Id}", exception);
            }
        }

        IReadOnlyList<ActionCondition> conditions = control.Conditions ?? [];
        for (int index = 0; index < conditions.Count; index++)
        {
            ActionCondition? condition = conditions[index];
            if (condition is null)
            {
                throw AppError.Internal($"projected action {control.Id} condition {index} is missing");
            }

            ActionConditionResult result;
            try
            {
                result = await condition(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                AppError.Find(exception) is null && !AppError.IsCancellation(exception))
            {
                throw AppError.Internal($"evaluate projected action {control.Id} condition {index}", exception);
            }

            if (!result.Available)
            {
                return new ActionState(
                    control.Id,
                    Visible: true,
                    Enabled: false,
                    result.DisabledReason);
            }
        }

        return new ActionState(control.Id, Visible: true, Enabled: true);
    }
}
