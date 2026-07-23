using System;
using System.Collections.Generic;

using Assimalign.Viu.Testing;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Store.Tests;

// Pins Pinia's $patch (https://pinia.vuejs.org/core-concepts/state.html#Mutating-the-state,
// packages/pinia/src/store.ts): the mutator and partial-state forms apply a group of mutations and
// notify $subscribe callbacks exactly once per patch (not once per member write), carrying the store
// id and patch kind. Notification rides the runtime scheduler's batched flush, so the pump makes it
// deterministic; counts (not just values) prove the single pass.
public sealed class StorePatchTests : IDisposable
{
    private readonly TestSchedulerPump _pump = TestSchedulerPump.Install();
    private readonly StoreRegistry _registry = new();

    public void Dispose()
    {
        _pump.RunUntilIdle();
        _registry.Dispose();
        _pump.Dispose();
    }

    private ModelCounterStore Resolve()
        => Stores.DefineStore("model-counter", () => new ModelCounterStore()).UseStore(_registry);

    [Fact]
    public void Patch_MutatorForm_AppliesEveryMutation()
    {
        var store = Resolve();

        store.Patch(state =>
        {
            state.Count = 5;
            state.Step = 3;
        });

        store.State.Count.ShouldBe(5);
        store.State.Step.ShouldBe(3);
    }

    [Fact]
    public void Patch_MutatorForm_NotifiesSubscribersExactlyOnce_NotOncePerMember()
    {
        var store = Resolve();
        var mutations = new List<StoreMutation>();
        store.Subscribe((mutation, _) => mutations.Add(mutation), detached: true);

        store.Patch(state =>
        {
            state.Count = 5;             // two member writes in one patch
            state.Step = 3;
        });
        _pump.RunUntilIdle();

        mutations.Count.ShouldBe(1);     // one notification pass, not one per member
        mutations[0].StoreId.ShouldBe("model-counter");
        mutations[0].Kind.ShouldBe(StorePatchKind.PatchFunction);
    }

    [Fact]
    public void Patch_MutatorForm_SubscriberSeesTheUpdatedState()
    {
        var store = Resolve();
        var seen = -1;
        store.Subscribe((_, state) => seen = state.Count, detached: true);

        store.Patch(state => state.Count = 42);
        _pump.RunUntilIdle();

        seen.ShouldBe(42);
    }

    [Fact]
    public void Patch_ObjectForm_MergesViaApplier_AndNotifiesWithPatchObjectKind()
    {
        var store = Resolve();
        var mutations = new List<StoreMutation>();
        store.Subscribe((mutation, _) => mutations.Add(mutation), detached: true);

        store.Patch(new CounterState { Count = 9, Step = 2 });
        _pump.RunUntilIdle();

        store.State.Count.ShouldBe(9);
        store.State.Step.ShouldBe(2);
        mutations.Count.ShouldBe(1);
        mutations[0].Kind.ShouldBe(StorePatchKind.PatchObject);
    }

    [Fact]
    public void Patch_ObjectForm_OnAStoreWithoutAnApplier_Throws()
    {
        var store = new NoResetStore();

        Should.Throw<NotSupportedException>(() => store.Patch(new CounterState { Count = 1, Step = 1 }));
    }

    [Fact]
    public void DirectStateWrites_InOneTurn_CoalesceIntoOneNotification()
    {
        var store = Resolve();
        var mutations = new List<StoreMutation>();
        store.Subscribe((mutation, _) => mutations.Add(mutation), detached: true);

        store.State.Count = 1;           // two direct writes (outside Patch) in one turn
        store.State.Step = 9;
        _pump.RunUntilIdle();

        mutations.Count.ShouldBe(1);     // the scheduler-batched flush collapses them to one pass
        mutations[0].Kind.ShouldBe(StorePatchKind.Direct);
    }

    [Fact]
    public void NoOpPatch_DoesNotLeakItsKind_OntoALaterDirectWrite()
    {
        var store = Resolve();
        var mutations = new List<StoreMutation>();
        store.Subscribe((mutation, _) => mutations.Add(mutation), detached: true);

        store.Patch(state => state.Count = state.Count);   // equal value: no change, so no notification
        _pump.RunUntilIdle();
        mutations.Count.ShouldBe(0);

        store.State.Count = 7;                             // a real direct write afterwards
        _pump.RunUntilIdle();

        mutations.Count.ShouldBe(1);
        mutations[0].Kind.ShouldBe(StorePatchKind.Direct); // Direct, not the earlier patch's stale kind
    }

    [Fact]
    public void Patch_NullMutator_Throws()
    {
        var store = Resolve();

        Should.Throw<ArgumentNullException>(() => store.Patch((Action<CounterState>)null!));
    }
}
