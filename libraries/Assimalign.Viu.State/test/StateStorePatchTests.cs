using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStorePatchTests
{
    [Fact]
    public void Patch_MutatorForm_AppliesAllWritesAndSchedulesOneNotification()
    {
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry(scheduler);
        ModelCounterStateStore stateStore = Resolve(registry);
        List<StateStoreMutation> mutations = new();
        stateStore.Subscribe(
            (mutation, _) => mutations.Add(mutation),
            detached: true);

        stateStore.Patch(
            state =>
            {
                state.Count = 5;
                state.Step = 3;
            });

        stateStore.State.Count.ShouldBe(5);
        stateStore.State.Step.ShouldBe(3);
        mutations.ShouldBeEmpty();
        scheduler.PendingCount.ShouldBe(1);

        scheduler.RunUntilIdle();

        mutations.Count.ShouldBe(1);
        mutations[0].StateStoreKey.ShouldBe("model-counter");
        mutations[0].Kind.ShouldBe(
            StateStorePatchKind.PatchFunction);
    }

    [Fact]
    public void Patch_ObjectForm_UsesTypedApplierAndReportsPatchObject()
    {
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry(scheduler);
        ModelCounterStateStore stateStore = Resolve(registry);
        List<StateStoreMutation> mutations = new();
        stateStore.Subscribe(
            (mutation, _) => mutations.Add(mutation),
            detached: true);

        stateStore.Patch(
            new CounterState
            {
                Count = 9,
                Step = 2,
            });
        scheduler.RunUntilIdle();

        stateStore.State.Count.ShouldBe(9);
        stateStore.State.Step.ShouldBe(2);
        mutations.Count.ShouldBe(1);
        mutations[0].Kind.ShouldBe(
            StateStorePatchKind.PatchObject);
    }

    [Fact]
    public void ScheduledDirectWrites_InOneTurn_CoalesceUntilFlush()
    {
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry(scheduler);
        ModelCounterStateStore stateStore = Resolve(registry);
        List<StateStoreMutation> mutations = new();
        stateStore.Subscribe(
            (mutation, _) => mutations.Add(mutation),
            detached: true);

        stateStore.State.Count = 1;
        stateStore.State.Step = 9;

        mutations.ShouldBeEmpty();
        scheduler.PendingCount.ShouldBe(1);

        scheduler.RunUntilIdle();

        mutations.Count.ShouldBe(1);
        mutations[0].Kind.ShouldBe(StateStorePatchKind.Direct);
    }

    [Fact]
    public void SynchronousFallback_DeliversEachDirectWriteImmediately()
    {
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        ModelCounterStateStore stateStore = Resolve(registry);
        List<StateStoreMutation> mutations = new();
        stateStore.Subscribe(
            (mutation, _) => mutations.Add(mutation),
            detached: true);

        stateStore.State.Count = 1;
        mutations.Count.ShouldBe(1);

        stateStore.State.Step = 9;
        mutations.Count.ShouldBe(2);
        mutations.ShouldAllBe(
            mutation => mutation.Kind == StateStorePatchKind.Direct);
    }

    [Fact]
    public void SynchronousFallback_GroupedPatchStillNotifiesOnce()
    {
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        ModelCounterStateStore stateStore = Resolve(registry);
        List<StateStoreMutation> mutations = new();
        stateStore.Subscribe(
            (mutation, _) => mutations.Add(mutation),
            detached: true);

        stateStore.Patch(
            state =>
            {
                state.Count = 4;
                state.Step = 2;
            });

        mutations.Count.ShouldBe(1);
        mutations[0].Kind.ShouldBe(
            StateStorePatchKind.PatchFunction);
    }

    [Fact]
    public void NoOpPatch_DoesNotLeakPatchKindOntoLaterDirectWrite()
    {
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry(scheduler);
        ModelCounterStateStore stateStore = Resolve(registry);
        List<StateStoreMutation> mutations = new();
        stateStore.Subscribe(
            (mutation, _) => mutations.Add(mutation),
            detached: true);

        stateStore.Patch(
            state => state.Count = state.Count);
        scheduler.RunUntilIdle();
        mutations.ShouldBeEmpty();

        stateStore.State.Count = 7;
        scheduler.RunUntilIdle();

        mutations.Count.ShouldBe(1);
        mutations[0].Kind.ShouldBe(StateStorePatchKind.Direct);
    }

    [Fact]
    public void QueuedChanges_CarryTheLatestPatchKindInTheFlush()
    {
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry(scheduler);
        ModelCounterStateStore stateStore = Resolve(registry);
        List<StateStoreMutation> mutations = new();
        stateStore.Subscribe(
            (mutation, _) => mutations.Add(mutation),
            detached: true);

        stateStore.Patch(state => state.Count = 2);
        stateStore.Patch(
            new CounterState
            {
                Count = 3,
                Step = 1,
            });
        scheduler.RunUntilIdle();

        mutations.Count.ShouldBe(1);
        mutations[0].Kind.ShouldBe(
            StateStorePatchKind.PatchObject);
    }

    [Fact]
    public void Patch_NullOrUnsupportedForms_Throw()
    {
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        ModelCounterStateStore stateStore = Resolve(registry);
        NoResetStateStore noResetStateStore = new();

        Should.Throw<ArgumentNullException>(
            () => stateStore.Patch((Action<CounterState>)null!));
        Should.Throw<ArgumentNullException>(
            () => stateStore.Patch((CounterState)null!));
        Should.Throw<NotSupportedException>(
            () => noResetStateStore.Patch(new CounterState()));
    }

    private static ModelCounterStateStore Resolve(
        StateStoreRegistry registry)
    {
        StateStoreDefinition<ModelCounterStateStore> definition =
            StateStores.Define(
                "model-counter",
                static () => new ModelCounterStateStore());
        return definition.Use(registry);
    }
}
