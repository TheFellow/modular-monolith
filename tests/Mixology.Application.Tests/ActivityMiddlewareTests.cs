using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mixology.Application.Auditing;
using Mixology.Application.Authentication;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Xunit;
using StoreFixture = Mixology.Application.Tests.UnitOfWorkMiddlewareTests.StoreFixture;

namespace Mixology.Application.Tests;

public sealed class ActivityMiddlewareTests
{
    [Fact]
    public async Task SuccessIsRecordedInsideTheBusinessTransaction()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        RecordingActivityRecorder recorder = new();
        OperationChain chain = CreateChain(fixture, recorder);
        EntityUid touched = new("Mixology::Ingredient", "ing-one");

        await chain.ExecuteAsync(
            new OperationContext(Actor.Owner),
            Operation.Command("Ingredient.create"),
            async context =>
            {
                context.Touch(touched);
                await InsertAsync(context, "success");
            });

        OperationActivity activity = Assert.Single(recorder.Activities);
        Assert.True(Assert.Single(recorder.TransactionStates));
        Assert.True(activity.Success);
        Assert.Equal(touched, activity.Resource);
        Assert.Equal([touched], activity.Touches);
        Assert.Equal(1, await fixture.CountAsync());
    }

    [Fact]
    public async Task FailureRollsBackThenRecordsSeparatelyAndPreservesTheOriginalError()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        RecordingActivityRecorder recorder = new();
        OperationChain chain = CreateChain(fixture, recorder);
        InvalidError expected = AppError.Invalid("bad ingredient");

        InvalidError actual = await Assert.ThrowsAsync<InvalidError>(() => chain.ExecuteAsync(
            new OperationContext(Actor.Owner),
            Operation.Command("Ingredient.create"),
            async context =>
            {
                await InsertAsync(context, "rollback");
                throw expected;
            }));

        Assert.Same(expected, actual);
        OperationActivity activity = Assert.Single(recorder.Activities);
        Assert.True(Assert.Single(recorder.TransactionStates));
        Assert.False(activity.Success);
        Assert.Equal(ErrorKind.Invalid, activity.ErrorKind);
        Assert.Equal("bad ingredient", activity.Error);
        Assert.Equal(0, await fixture.CountAsync());
    }

    [Fact]
    public async Task SuccessAuditFailureRollsBackAndIsNotRetriedAsAFailureAudit()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        ThrowingActivityRecorder recorder = new();
        OperationChain chain = CreateChain(fixture, recorder);

        InternalError error = await Assert.ThrowsAsync<InternalError>(() => chain.ExecuteAsync(
            new OperationContext(Actor.Owner),
            Operation.Command("Ingredient.create"),
            context => InsertAsync(context, "rollback")));

        Assert.Equal("record activity", error.Message);
        Assert.Equal(1, recorder.Attempts);
        Assert.Equal(0, await fixture.CountAsync());
    }

    [Fact]
    public async Task TransactionFailureAfterStagedSuccessIsRecordedAsAFailedAttempt()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        RecordingActivityRecorder recorder = new();
        TimeProvider timeProvider = TimeProvider.System;
        TrackActivityMiddleware track = new(
            fixture.Store,
            recorder,
            timeProvider,
            NullLogger<TrackActivityMiddleware>.Instance);
        RecordSuccessfulActivityMiddleware success = new(recorder, timeProvider);
        ConflictError expected = AppError.Conflict("unique constraint");
        OperationChain chain = new(
        [
            track.InvokeAsync,
            async (context, operation, next) =>
            {
                await next(context).ConfigureAwait(false);
                throw expected;
            },
            success.InvokeAsync,
        ]);

        ConflictError actual = await Assert.ThrowsAsync<ConflictError>(() => chain.ExecuteAsync(
            new OperationContext(Actor.Owner),
            Operation.Command("Ingredient.create"),
            _ => Task.CompletedTask));

        Assert.Same(expected, actual);
        Assert.Equal(2, recorder.Activities.Count);
        Assert.False(recorder.Activities[^1].Success);
        Assert.Equal(ErrorKind.Conflict, recorder.Activities[^1].ErrorKind);
    }

    [Fact]
    public async Task FailureAuditFailureDoesNotMaskTheCommandError()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        ThrowingActivityRecorder recorder = new();
        OperationChain chain = CreateChain(fixture, recorder);
        ConflictError expected = AppError.Conflict("duplicate name");

        ConflictError actual = await Assert.ThrowsAsync<ConflictError>(() => chain.ExecuteAsync(
            new OperationContext(Actor.Owner),
            Operation.Command("Ingredient.create"),
            _ => throw expected));

        Assert.Same(expected, actual);
        Assert.Equal(1, recorder.Attempts);
    }

    [Fact]
    public async Task CallerOwnedSuccessLeavesWorkAndAuditPending()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        await using Mixology.Persistence.StoreSession session = await fixture.Store.OpenSessionAsync();
        await session.BeginWriteAsync();
        RecordingActivityRecorder recorder = new();
        OperationChain chain = CreateChain(fixture, recorder);

        await chain.ExecuteAsync(
            new OperationContext(Actor.Owner, session),
            Operation.Command("Ingredient.create"),
            context => InsertAsync(context, "pending"));

        Assert.True(session.HasTransaction);
        Assert.Single(recorder.Activities);
        await session.RollbackAsync();
        Assert.Equal(0, await fixture.CountAsync());
    }

    [Fact]
    public async Task CallerOwnedFailureLeavesFailureAuditInTheCallerTransaction()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync();
        await using Mixology.Persistence.StoreSession session = await fixture.Store.OpenSessionAsync();
        await session.BeginWriteAsync();
        RecordingActivityRecorder recorder = new();
        OperationChain chain = CreateChain(fixture, recorder);
        InvalidError expected = AppError.Invalid("failed");

        InvalidError actual = await Assert.ThrowsAsync<InvalidError>(() => chain.ExecuteAsync(
            new OperationContext(Actor.Owner, session),
            Operation.Command("Ingredient.create"),
            async context =>
            {
                await InsertAsync(context, "still pending");
                throw expected;
            }));

        Assert.Same(expected, actual);
        Assert.True(session.HasTransaction);
        Assert.False(Assert.Single(recorder.Activities).Success);
        await session.RollbackAsync();
        Assert.Equal(0, await fixture.CountAsync());
    }

    private static OperationChain CreateChain(StoreFixture fixture, IActivityRecorder recorder)
    {
        TimeProvider timeProvider = TimeProvider.System;
        TrackActivityMiddleware track = new(
            fixture.Store,
            recorder,
            timeProvider,
            NullLogger<TrackActivityMiddleware>.Instance);
        UnitOfWorkMiddleware unitOfWork = new(fixture.Store);
        RecordSuccessfulActivityMiddleware success = new(recorder, timeProvider);
        return new([track.InvokeAsync, unitOfWork.InvokeAsync, success.InvokeAsync]);
    }

    private static Task<int> InsertAsync(OperationContext context, string value) =>
        context.Session?.Context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO operation_probe (value) VALUES ({value})",
            context.CancellationToken)
        ?? throw new InvalidOperationException("Command did not receive a store session.");

    private sealed class RecordingActivityRecorder : IActivityRecorder
    {
        public List<OperationActivity> Activities { get; } = [];
        public List<bool> TransactionStates { get; } = [];

        public Task RecordAsync(OperationContext context, OperationActivity activity)
        {
            Activities.Add(activity);
            TransactionStates.Add(context.Session?.HasTransaction == true);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingActivityRecorder : IActivityRecorder
    {
        public int Attempts { get; private set; }

        public Task RecordAsync(OperationContext context, OperationActivity activity)
        {
            _ = context;
            _ = activity;
            Attempts++;
            throw new IOException("audit unavailable");
        }
    }
}
