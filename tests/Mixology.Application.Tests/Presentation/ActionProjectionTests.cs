using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Errors;
using Xunit;

namespace Mixology.Application.Tests.Presentation;

public sealed class ActionProjectionTests
{
    [Fact]
    public async Task DeniedControlIsHiddenWithoutEvaluatingConditions()
    {
        bool evaluated = false;
        ActionGroup declaration = new(
            Controls:
            [
                new ActionControl(
                    new ActionId("menus.publish"),
                    ActionPermission.Require(_ => ValueTask.FromException(AppError.Permission("denied"))),
                    [Condition]),
            ]);

        IReadOnlyList<ActionState> states = await ActionProjector.EvaluateAsync(declaration);

        ActionState state = Assert.Single(states);
        Assert.False(state.Visible);
        Assert.False(state.Enabled);
        Assert.False(evaluated);
        return;

        ValueTask<ActionConditionResult> Condition(CancellationToken _)
        {
            evaluated = true;
            return ValueTask.FromResult(ActionConditionResult.Enabled);
        }
    }

    [Fact]
    public async Task AuthorizedControlStopsAtFirstUnavailableCondition()
    {
        int evaluated = 0;
        ActionGroup declaration = new(
            Controls:
            [
                new ActionControl(
                    new ActionId("menus.publish"),
                    ActionPermission.Require(_ => ValueTask.CompletedTask),
                    [
                        _ =>
                        {
                            evaluated++;
                            return ValueTask.FromResult(
                                ActionConditionResult.Disabled("Add a drink before publishing."));
                        },
                        _ =>
                        {
                            evaluated++;
                            return ValueTask.FromResult(ActionConditionResult.Enabled);
                        },
                    ]),
            ]);

        ActionState state = Assert.Single(await ActionProjector.EvaluateAsync(declaration));

        Assert.True(state.Visible);
        Assert.False(state.Enabled);
        Assert.Equal("Add a drink before publishing.", state.DisabledReason);
        Assert.Equal(1, evaluated);
    }

    [Fact]
    public async Task ControlAndNestedGroupPermissionsOverrideTheirParent()
    {
        int parentChecks = 0;
        int childChecks = 0;
        ActionPermission parent = ActionPermission.Require(_ =>
        {
            parentChecks++;
            return ValueTask.FromException(AppError.Permission("parent denied"));
        });
        ActionGroup declaration = new(
            parent,
            Controls:
            [
                new ActionControl(new ActionId("inherited")),
                new ActionControl(new ActionId("public"), ActionPermission.Public),
                new ActionControl(
                    new ActionId("override"),
                    ActionPermission.Require(_ =>
                    {
                        childChecks++;
                        return ValueTask.CompletedTask;
                    })),
            ],
            Groups:
            [
                new ActionGroup(
                    ActionPermission.Public,
                    [new ActionControl(new ActionId("nested-public"))]),
            ]);

        IReadOnlyList<ActionState> states = await ActionProjector.EvaluateAsync(declaration);

        Assert.False(states[0].Visible);
        Assert.True(states[1].Enabled);
        Assert.True(states[2].Enabled);
        Assert.True(states[3].Enabled);
        Assert.Equal(1, parentChecks);
        Assert.Equal(1, childChecks);
    }

    [Fact]
    public async Task EvaluatorFailuresRemainStronglyTypedAndCancellationRemainsCancellation()
    {
        ActionGroup typed = new(
            Controls:
            [
                new ActionControl(
                    new ActionId("typed"),
                    ActionPermission.Require(_ => ValueTask.FromException(AppError.Conflict("policy unavailable")))),
            ]);
        ActionGroup unknown = new(
            Controls:
            [
                new ActionControl(
                    new ActionId("unknown"),
                    Conditions: [_ => ValueTask.FromException<ActionConditionResult>(new IOException("secret"))]),
            ]);
        ActionGroup cancelled = new(
            Controls:
            [
                new ActionControl(
                    new ActionId("cancelled"),
                    ActionPermission.Require(_ => ValueTask.FromException(new TaskCanceledException()))),
            ]);

        Exception typedError = await Assert.ThrowsAsync<ConflictError>(
            () => ActionProjector.EvaluateAsync(typed));
        InternalError unknownError = await Assert.ThrowsAsync<InternalError>(
            () => ActionProjector.EvaluateAsync(unknown));
        Exception cancellation = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ActionProjector.EvaluateAsync(cancelled));

        Assert.True(AppError.IsConflict(typedError));
        Assert.NotNull(AppError.Find<IOException>(unknownError));
        Assert.True(AppError.IsCancellation(cancellation));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyAndDuplicateIdsAreInternalDeclarationErrors(string empty)
    {
        ActionGroup emptyDeclaration = new(Controls: [new ActionControl(new ActionId(empty))]);
        ActionId repeated = new("same");
        ActionGroup duplicateDeclaration = new(
            Controls: [new ActionControl(repeated)],
            Groups: [new ActionGroup(Controls: [new ActionControl(repeated)])]);

        await Assert.ThrowsAsync<InternalError>(() => ActionProjector.EvaluateAsync(emptyDeclaration));
        await Assert.ThrowsAsync<InternalError>(() => ActionProjector.EvaluateAsync(duplicateDeclaration));
    }
}
